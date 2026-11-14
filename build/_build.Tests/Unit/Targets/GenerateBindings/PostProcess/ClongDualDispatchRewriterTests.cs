using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;
using Microsoft.CodeAnalysis.CSharp;

namespace Build.Tests.Unit.Targets.GenerateBindings.PostProcess;

public sealed class ClongDualDispatchRewriterTests
{
    [Test]
    public async Task ParseAttributeArgumentList_Should_Preserve_Import_And_CallConv_Arguments()
    {
        var import = SyntaxFactory.ParseAttributeArgumentList("(\"SDL2\", EntryPoint = \"SDL_ThreadID\")");
        var callConv = SyntaxFactory.ParseAttributeArgumentList("(CallConvs = new[] { typeof(CallConvCdecl) })");
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

    private static string Rewrite(string fixture, ClongDualDispatchRewriter.Mode mode, string[] methods)
        => PostProcessFixture.Rewrite(PostProcessFixture.Source(fixture), new ClongDualDispatchRewriter(mode, methods));
}
