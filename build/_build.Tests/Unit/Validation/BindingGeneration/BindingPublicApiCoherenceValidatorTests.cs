using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Validation.BindingGeneration;

namespace Build.Tests.Unit.Validation.BindingGeneration;

public sealed class BindingPublicApiCoherenceValidatorTests
{
    private static readonly DynapiManifest TwoSymbolsManifest = new(
        PublicSymbols: new HashSet<string>(StringComparer.Ordinal) { "SDL_Init", "SDL_Quit" },
        Origin: DynapiManifestOrigin.VcpkgBuildtree,
        SourcePath: "/test/SDL2.exports");

    [Test]
    public async Task Validate_Should_Return_Empty_Report_When_Emit_Matches_Manifest_Exactly()
    {
        var validator = new BindingPublicApiCoherenceValidator();
        var emitted = new HashSet<string>(StringComparer.Ordinal) { "SDL_Init", "SDL_Quit" };

        var report = validator.Validate(emitted, TwoSymbolsManifest, SeverityProfile.Stage1Generator);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Flag_False_Positive_As_Error_Under_Stage1_Profile()
    {
        var validator = new BindingPublicApiCoherenceValidator();
        var emitted = new HashSet<string>(StringComparer.Ordinal) { "SDL_Init", "SDL_Quit", "SDL_RectEmpty" };

        var report = validator.Validate(emitted, TwoSymbolsManifest, SeverityProfile.Stage1Generator);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Severity).IsEqualTo(ValidationSeverity.Error);
        await Assert.That(report.Errors[0].Message).Contains("SDL_RectEmpty");
        await Assert.That(report.Errors[0].Message).Contains("dynapi manifest");
    }

    [Test]
    public async Task Validate_Should_Flag_False_Negative_As_Warning_Under_Stage1_Profile()
    {
        var validator = new BindingPublicApiCoherenceValidator();
        var emitted = new HashSet<string>(StringComparer.Ordinal) { "SDL_Init" };

        var report = validator.Validate(emitted, TwoSymbolsManifest, SeverityProfile.Stage1Generator);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.HasWarnings).IsTrue();
        await Assert.That(report.Warnings.Count).IsEqualTo(1);
        await Assert.That(report.Warnings[0].Message).Contains("SDL_Quit");
    }

    [Test]
    public async Task Validate_Should_Flag_False_Negative_As_Error_Under_Stage2_Strict_Profile()
    {
        var validator = new BindingPublicApiCoherenceValidator();
        var emitted = new HashSet<string>(StringComparer.Ordinal) { "SDL_Init" };

        var report = validator.Validate(emitted, TwoSymbolsManifest, SeverityProfile.Stage2Strict);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Message).Contains("SDL_Quit");
    }

    [Test]
    public async Task Validate_Should_Detect_Both_Directions_Simultaneously()
    {
        var validator = new BindingPublicApiCoherenceValidator();
        var emitted = new HashSet<string>(StringComparer.Ordinal) { "SDL_Init", "SDL_LeakedInline" };

        var report = validator.Validate(emitted, TwoSymbolsManifest, SeverityProfile.Stage2Strict);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(2);
        await Assert.That(report.Errors.Any(c => c.Message.Contains("SDL_Quit", StringComparison.Ordinal))).IsTrue();
        await Assert.That(report.Errors.Any(c => c.Message.Contains("SDL_LeakedInline", StringComparison.Ordinal))).IsTrue();
    }
}
