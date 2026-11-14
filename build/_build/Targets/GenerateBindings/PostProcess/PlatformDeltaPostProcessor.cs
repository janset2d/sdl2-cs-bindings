using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace Janset.SDL2.PostProcess;

internal sealed class PlatformDeltaPostProcessor
{
    private readonly string[] _platformOrder;
    private readonly IReadOnlyDictionary<string, string> _supportedOsByPlatform;

    public PlatformDeltaPostProcessor(IReadOnlyList<Config.PlatformView> views)
    {
        _platformOrder = views.Select(v => v.Name).ToArray();
        _supportedOsByPlatform = views.ToDictionary(v => v.Name, v => v.SupportedOs, StringComparer.Ordinal);
    }

    public PlatformDeltaResult Process(string inputDir, string outputDir)
    {
        var generatedRoot = new DirectoryInfo(inputDir);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var processed = 0;
        var transformed = 0;
        var removed = 0;

        foreach (var file in EnumerateNeutralFiles(generatedRoot))
        {
            CollectDeclarations(file, seen);
            CopyPassthroughFile(inputDir, outputDir, file);
        }

        foreach (var file in EnumeratePlatformFiles(generatedRoot))
        {
            processed++;

            var relative = Path.GetRelativePath(inputDir, file.FullName);
            var source = File.ReadAllText(file.FullName);
            var platformName = GetPlatformName(relative);
            var supportedOs = _supportedOsByPlatform[platformName];

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();
            var rewriter = new PlatformDeltaFileRewriter(seen, supportedOs);
            var rewritten = (CompilationUnitSyntax)rewriter.Visit(root)!;
            var output = rewritten.ToFullString();
            if (rewriter.AddedPlatformAttributes)
            {
                output = EnsureVersioningUsing(output);
                output = GuardPlatformAttributes(output);
            }

            if (rewriter.AnyChanges || !string.Equals(inputDir, outputDir, StringComparison.OrdinalIgnoreCase))
            {
                var destination = Path.Combine(outputDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                PostProcessCli.WriteAllTextLf(destination, output);
            }

            if (rewriter.AnyChanges)
            {
                transformed++;
                removed += rewriter.RemovedDeclarations;
            }
        }

        return new PlatformDeltaResult(processed, transformed, removed);
    }

    private static void CopyPassthroughFile(string inputDir, string outputDir, FileInfo file)
    {
        if (string.Equals(inputDir, outputDir, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relative = Path.GetRelativePath(inputDir, file.FullName);
        var destination = Path.Combine(outputDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        PostProcessCli.WriteAllTextLf(destination, File.ReadAllText(file.FullName));
    }

    private static IEnumerable<FileInfo> EnumerateNeutralFiles(DirectoryInfo generatedRoot)
    {
        return generatedRoot
            .EnumerateFiles("*.g.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(generatedRoot.FullName, file.FullName)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Contains("Platforms", StringComparer.Ordinal));
    }

    private IEnumerable<FileInfo> EnumeratePlatformFiles(DirectoryInfo generatedRoot)
    {
        var platformRoot = new DirectoryInfo(Path.Combine(generatedRoot.FullName, "Platforms"));
        if (!platformRoot.Exists)
        {
            return [];
        }

        return _platformOrder
            .SelectMany(platform => platformRoot
                .EnumerateFiles("*.g.cs", SearchOption.AllDirectories)
                .Where(file => Path.GetRelativePath(platformRoot.FullName, file.FullName)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0]
                    .Equals(platform, StringComparison.Ordinal))
                .OrderBy(file => file.FullName, StringComparer.Ordinal));
    }

    private static string GetPlatformName(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == "Platforms")
            {
                return parts[i + 1];
            }
        }

        throw new InvalidOperationException($"Platform file is not under a Platforms/<View> directory: {relativePath}");
    }

    private static void CollectDeclarations(FileInfo file, ISet<string> seen)
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file.FullName)).GetCompilationUnitRoot();
        var collector = new DeclarationCollector(seen);
        collector.Visit(root);
    }

    private static string EnsureVersioningUsing(string source)
    {
        if (source.Contains("System.Runtime.Versioning", StringComparison.Ordinal))
        {
            return source;
        }

        const string marker = "using System.Runtime.InteropServices;\n";
        const string guardedUsing = "using System.Runtime.InteropServices;\n#if NET5_0_OR_GREATER\nusing System.Runtime.Versioning;\n#endif\n";
        return source.Replace(marker, guardedUsing, StringComparison.Ordinal);
    }

    private static string GuardPlatformAttributes(string source)
    {
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.TrimEnd('\n').Split('\n');
        var builder = new StringBuilder(source.Length + lines.Length * 32);

        foreach (var line in lines)
        {
            if (line.Contains("[SupportedOSPlatform(", StringComparison.Ordinal))
            {
                var indentLength = line.Length - line.TrimStart().Length;
                var indent = line[..indentLength];
                builder.Append(indent).AppendLine("#if NET5_0_OR_GREATER");
                builder.AppendLine(line);
                builder.Append(indent).AppendLine("#endif");
                continue;
            }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }
}

internal readonly record struct PlatformDeltaResult(
    int ProcessedFiles,
    int TransformedFiles,
    int RemovedDeclarations);

internal sealed class PlatformDeltaFileRewriter(
    ISet<string> seenDeclarations,
    string supportedOs) : CSharpSyntaxRewriter
{
    public bool AnyChanges { get; private set; }

    public bool AddedPlatformAttributes { get; private set; }

    public int RemovedDeclarations { get; private set; }

    public override SyntaxNode? VisitDelegateDeclaration(DelegateDeclarationSyntax node)
    {
        return KeepOrRemove(DeclarationKey.Type(node.Identifier.Text), node, node.Identifier.Text);
    }

    public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        return KeepOrRemove(DeclarationKey.Type(node.Identifier.Text), node, node.Identifier.Text);
    }

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        return KeepOrRemove(DeclarationKey.Type(node.Identifier.Text), node, node.Identifier.Text);
    }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var rewritten = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
        return rewritten;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var key = DeclarationKey.Method(node.Identifier.Text);
        if (!seenDeclarations.Add(key))
        {
            AnyChanges = true;
            RemovedDeclarations++;
            return null;
        }

        if (HasPlatformAttribute(node))
        {
            return node;
        }

        AnyChanges = true;
        AddedPlatformAttributes = true;
        return node.WithAttributeLists(node.AttributeLists.Insert(0, CreatePlatformAttribute(node)));
    }

    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        var variables = node.Declaration.Variables;
        if (variables.Count != 1)
        {
            return node;
        }

        return KeepOrRemove(DeclarationKey.Field(node, variables[0].Identifier.Text), node, variables[0].Identifier.Text);
    }

    private SyntaxNode? KeepOrRemove<TNode>(string key, TNode node, string name)
        where TNode : SyntaxNode
    {
        _ = name;
        if (seenDeclarations.Add(key))
        {
            return node;
        }

        AnyChanges = true;
        RemovedDeclarations++;
        return null;
    }

    private AttributeListSyntax CreatePlatformAttribute(MethodDeclarationSyntax method)
    {
        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("SupportedOSPlatform"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(supportedOs))))));

        return SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            .NormalizeWhitespace()
            .WithLeadingTrivia(method.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.LineFeed);
    }

    private static bool HasPlatformAttribute(MethodDeclarationSyntax method)
    {
        return method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attr => attr.Name.ToString().Contains("SupportedOSPlatform", StringComparison.Ordinal));
    }
}

internal sealed class DeclarationCollector(ISet<string> seenDeclarations) : CSharpSyntaxWalker
{
    public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
    {
        seenDeclarations.Add(DeclarationKey.Type(node.Identifier.Text));
        base.VisitDelegateDeclaration(node);
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        seenDeclarations.Add(DeclarationKey.Type(node.Identifier.Text));
        base.VisitEnumDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        seenDeclarations.Add(DeclarationKey.Type(node.Identifier.Text));
        base.VisitStructDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        seenDeclarations.Add(DeclarationKey.Method(node.Identifier.Text));
        base.VisitMethodDeclaration(node);
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        foreach (var variable in node.Declaration.Variables)
        {
            seenDeclarations.Add(DeclarationKey.Field(node, variable.Identifier.Text));
        }

        base.VisitFieldDeclaration(node);
    }
}

internal static class DeclarationKey
{
    public static string Type(string name) => $"type:{name}";

    public static string Method(string name) => $"method:{name}";

    public static string Field(SyntaxNode node, string name)
    {
        var declaringType = string.Join(
            ".",
            node.Ancestors()
                .OfType<BaseTypeDeclarationSyntax>()
                .Reverse()
                .Select(type => type.Identifier.Text));

        return $"field:{declaringType}:{name}";
    }
}
