using Build.Targets.OtoolAnalyze.Services;

namespace Build.Tests.Unit.Targets.OtoolAnalyze;

public sealed class LibraryClassifierTests
{

    [Test]
    public async Task Classify_Should_Mark_System_When_Path_Starts_With_UsrLib()
    {
        var result = LibraryClassifier.Classify("libSystem.B.dylib", "/usr/lib/libSystem.B.dylib");
        await Assert.That(result.Kind).IsEqualTo(LibraryKind.System);
        await Assert.That(result.IsSystem).IsTrue();
    }

    [Test]
    public async Task Classify_Should_Mark_System_When_Path_Starts_With_SystemLibrary()
    {
        var result = LibraryClassifier.Classify("libobjc.A.dylib", "/System/Library/Frameworks/libobjc.A.dylib");
        await Assert.That(result.IsSystem).IsTrue();
    }

    [Test]
    public async Task Classify_Should_Mark_Framework_When_Name_Contains_Framework_Suffix_Under_System()
    {
        var result = LibraryClassifier.Classify("CoreFoundation.framework", "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation");
        await Assert.That(result.Kind).IsEqualTo(LibraryKind.Framework);
        await Assert.That(result.IsSystem).IsTrue();
    }

    [Test]
    public async Task Classify_Should_Mark_RPath_When_Path_Starts_With_RPath_Token()
    {
        var result = LibraryClassifier.Classify("libSDL2-2.0.0.dylib", "@rpath/libSDL2-2.0.0.dylib");
        await Assert.That(result.Kind).IsEqualTo(LibraryKind.RPath);
        await Assert.That(result.IsSystem).IsFalse();
    }

    [Test]
    public async Task Classify_Should_Mark_RPath_When_Path_Starts_With_LoaderPath_Token()
    {
        var result = LibraryClassifier.Classify("libfoo.dylib", "@loader_path/libfoo.dylib");
        await Assert.That(result.Kind).IsEqualTo(LibraryKind.RPath);
    }

    [Test]
    public async Task Classify_Should_Mark_RPath_When_Path_Starts_With_ExecutablePath_Token()
    {
        var result = LibraryClassifier.Classify("libbar.dylib", "@executable_path/libbar.dylib");
        await Assert.That(result.Kind).IsEqualTo(LibraryKind.RPath);
    }

    [Test]
    public async Task Classify_Should_Mark_System_From_CommonSystemLibraries_List()
    {
        var result = LibraryClassifier.Classify("libSystem.B.dylib", "/some/non-standard/path/libSystem.B.dylib");
        await Assert.That(result.IsSystem).IsTrue();
    }

    [Test]
    public async Task Classify_Should_Mark_User_When_No_System_Pattern_Matches()
    {
        var result = LibraryClassifier.Classify("libSDL2_image-2.0.0.dylib", "/Users/dev/sdk/lib/libSDL2_image-2.0.0.dylib");
        await Assert.That(result.Kind).IsEqualTo(LibraryKind.User);
        await Assert.That(result.IsSystem).IsFalse();
    }
}
