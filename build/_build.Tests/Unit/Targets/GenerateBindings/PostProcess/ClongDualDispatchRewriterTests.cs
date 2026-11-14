using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;
using Microsoft.CodeAnalysis.CSharp;

namespace Build.Tests.Unit.Targets.GenerateBindings.PostProcess;

public sealed class ClongDualDispatchRewriterTests
{
    [Test]
    public async Task ParseAttributeArgumentList_Should_Preserve_Import_And_CallConv_Arguments()
    {
        var import = SyntaxFactory.ParseAttributeArgumentList(PostProcessFixture.Source("importArguments"));
        var callConv = SyntaxFactory.ParseAttributeArgumentList(PostProcessFixture.Source("callConvArguments"));
        await Assert.That(import!.Arguments.Count).IsEqualTo(2);
        await Assert.That(callConv!.Arguments.Count).IsEqualTo(1);
        await Assert.That(callConv.ToFullString()).Contains("typeof(CallConvCdecl)");
    }

    [Test]
    public async Task VisitClassDeclaration_Should_Dispatch_Long_Parameters_For_Both_Backends()
    {
        var modern = Rewrite("clongFixture", ClongDualDispatchRewriter.Mode.Modern, ["TTF_OpenFontIndex"]);
        var compat = Rewrite("clongFixture", ClongDualDispatchRewriter.Mode.Compat, ["TTF_OpenFontIndex"]);
        await Assert.That(modern).Contains("CLong index");
        await Assert.That(modern).Contains("[LibraryImport");
        await Assert.That(compat).Contains("RuntimeInformation.IsOSPlatform");
        await Assert.That(compat).Contains("_Win32");
        await Assert.That(compat).Contains("_Unix64");
        await Assert.That(compat).Contains("(int)index");
        await Assert.That(compat).Contains("(nint)index");
    }

    [Test]
    public async Task VisitClassDeclaration_Should_Dispatch_Signed_Long_Returns_For_Both_Backends()
    {
        var modern = Rewrite("signedReturnFixture", ClongDualDispatchRewriter.Mode.Modern, ["TTF_FontFaces"]);
        var compat = Rewrite("signedReturnFixture", ClongDualDispatchRewriter.Mode.Compat, ["TTF_FontFaces"]);
        await Assert.That(modern).Contains("CLong TTF_FontFaces");
        await Assert.That(modern).Contains("[LibraryImport");
        await Assert.That(compat).Contains("RuntimeInformation.IsOSPlatform");
        await Assert.That(compat).Contains("_Win32");
        await Assert.That(compat).Contains("_Unix64");
        await Assert.That(compat).Contains("long TTF_FontFaces");
        await Assert.That(compat).Contains("int TTF_FontFaces_Win32");
        await Assert.That(compat).Contains("nint TTF_FontFaces_Unix64");
    }

    [Test]
    public async Task VisitClassDeclaration_Should_Preserve_ThreadId_Signatures_And_Member_Trivia()
    {
        var modern = Rewrite("threadIdFixture", ClongDualDispatchRewriter.Mode.Modern, ["SDL_ThreadID", "SDL_GetThreadID"]);
        var compat = Rewrite("threadIdFixture", ClongDualDispatchRewriter.Mode.Compat, ["SDL_ThreadID", "SDL_GetThreadID"]);
        await Assert.That(modern).Contains("CULong SDL_ThreadID");
        await Assert.That(modern).Contains("[LibraryImport(\"SDL2\", EntryPoint = \"SDL_ThreadID\")]");
        await Assert.That(modern).Contains("[return: NativeTypeName(\"SDL_threadID\")]");
        await Assert.That(modern).Contains("\n        [UnmanagedCallConv");
        await Assert.That(modern).Contains("\n        public static partial CULong SDL_ThreadID");
        await Assert.That(compat).Contains("ulong SDL_ThreadID");
        await Assert.That(compat).Contains("\n        public static ulong SDL_ThreadID");
        await Assert.That(compat).Contains("\n        private static extern uint SDL_ThreadID_Win32");
        await Assert.That(compat).Contains("\n        private static extern nint SDL_ThreadID_Unix64");
        await Assert.That(modern).DoesNotContain("();[LibraryImport");
        await Assert.That(compat).DoesNotContain("();[DllImport");
        await Assert.That(compat).DoesNotContain("();[return:");
        await Assert.That(compat).DoesNotContain("}[DllImport");
        await Assert.That(modern).Contains("public static partial CULong SDL_ThreadID();\n\n        [LibraryImport(\"SDL2\", EntryPoint = \"SDL_GetThreadID\")]");
        await Assert.That(compat).Contains("private static extern nint SDL_ThreadID_Unix64();\n\n        [return: NativeTypeName(\"SDL_threadID\")]");
    }

    [Test]
    [Arguments("Modern")]
    [Arguments("Compat")]
    public async Task VisitClassDeclaration_Should_Preserve_Empty_Nonmatching_And_Repeated_Configured_Output(string backend)
    {
        var config = await PostProcessFixture.ConfigAsync(PostProcessFixture.World());
        var mode = Enum.Parse<ClongDualDispatchRewriter.Mode>(backend);
        var methods = config.ClongMethods("ttf");
        string Apply(string source) => PostProcessFixture.Rewrite(source, new ClongDualDispatchRewriter(mode, methods));
        await Assert.That(Apply("")).IsEqualTo("");
        var nonmatching = PostProcessFixture.Source("source");
        await Assert.That(Apply(nonmatching)).IsEqualTo(nonmatching);
        var source = PostProcessFixture.Source("clongFixture");
        await Assert.That(PostProcessFixture.Rewrite(source, new ClongDualDispatchRewriter(mode, config.ClongMethods("image")))).IsEqualTo(source);
        var output = Apply(source);
        await Assert.That(output).Contains(backend == "Modern" ? "CLong index" : "RuntimeInformation.IsOSPlatform");
        await Assert.That(output).Contains("EntryPoint = \"TTF_OpenFontIndex\"");
        await Assert.That(Apply(output)).IsEqualTo(output);
    }

    [Test]
    [Arguments("Modern")]
    [Arguments("Compat")]
    public async Task VisitClassDeclaration_Should_Preserve_Returns_Trivia_And_Repeated_Configured_Output(string backend)
    {
        var config = await PostProcessFixture.ConfigAsync(PostProcessFixture.World());
        var mode = Enum.Parse<ClongDualDispatchRewriter.Mode>(backend);

        foreach (var fixture in new[] { "signedReturnFixture", "threadIdFixture", "clongTrivia" })
        {
            var family = fixture == "threadIdFixture" ? "core" : "ttf";
            string Apply(string source) => PostProcessFixture.Rewrite(source, new ClongDualDispatchRewriter(mode, config.ClongMethods(family)));
            var output = Apply(PostProcessFixture.Source(fixture));
            await Assert.That(output).Contains(backend == "Modern" ? "[LibraryImport(" : "RuntimeInformation.IsOSPlatform");

            if (fixture == "clongTrivia")
            {
                await Assert.That(output).Contains("// keep the C-long comment\n");
                await Assert.That(output).Contains("; // keep the tail");
            }

            await Assert.That(Apply(output)).IsEqualTo(output);
        }
    }

    private static string Rewrite(string fixture, ClongDualDispatchRewriter.Mode mode, string[] methods)
        => PostProcessFixture.Rewrite(PostProcessFixture.Source(fixture), new ClongDualDispatchRewriter(mode, methods));
}
