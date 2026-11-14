using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Tool;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Tools.ClangSharp;

public static class ClangSharpTool
{
    public static ClangSharpResult Run(ICakeContext context, ClangSharpSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        var result = new ClangSharpResult(0, [], []);
        var arguments = RenderArguments(settings);
        var toolArguments = new ProcessArgumentBuilder();
        toolArguments.AppendRange(arguments.Skip(1));
        context.DotNetTool(null, "tool", toolArguments, new DotNetToolSettings
        {
            WorkingDirectory = settings.WorkingDirectory,
            HandleExitCode = _ => true,
            SetupProcessSettings = processSettings =>
            {
                processSettings.RedirectStandardOutput = true;
                processSettings.RedirectStandardError = true;
            },
            PostAction = process => result = new ClangSharpResult(
                process.GetExitCode(),
                [.. process.GetStandardOutput()],
                [.. process.GetStandardError()]),
        });
        return result;
    }

    public static ProcessArgumentBuilder RenderArguments(ClangSharpSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var arguments = new ProcessArgumentBuilder();
        arguments.Append("tool");
        arguments.Append("run");
        arguments.Append("ClangSharpPInvokeGenerator");
        arguments.Append("--config");

        foreach (var config in settings.Config)
        {
            arguments.AppendQuoted(config);
        }

        foreach (var responseFile in settings.ResponseFiles)
        {
            arguments.AppendQuoted($"@{responseFile.FullPath}");
        }

        AppendOption(arguments, "--methodClassName", settings.MethodClassName);
        AppendOption(arguments, "--libraryPath", settings.LibraryPath);
        AppendOption(arguments, "--namespace", settings.Namespace);
        AppendOption(arguments, "--with-access-specifier", settings.AccessSpecifier);

        foreach (var includeDirectory in settings.IncludeDirectories)
        {
            AppendOption(arguments, "--include-directory", includeDirectory.FullPath);
        }

        AppendOption(arguments, "--file", settings.File.FullPath);
        AppendOption(arguments, "--output", settings.Output.FullPath);

        if (settings.Defines.Count > 0)
        {
            arguments.Append("--define-macro");

            foreach (var define in settings.Defines)
            {
                arguments.AppendQuoted(define);
            }
        }

        foreach (var undefine in settings.Undefines)
        {
            arguments.AppendQuoted($"--additional=--undefine-macro={undefine}");
        }

        if (settings.Exclusions.Count > 0)
        {
            arguments.Append("--exclude");

            foreach (var exclusion in settings.Exclusions)
            {
                arguments.AppendQuoted(exclusion);
            }
        }

        return arguments;
    }

    private static void AppendOption(ProcessArgumentBuilder arguments, string option, string value)
    {
        arguments.Append(option);
        arguments.AppendQuoted(value);
    }
}
