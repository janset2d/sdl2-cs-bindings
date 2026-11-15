using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class OwnershipAuditTests
{
    [Test]
    public async Task Run_Should_Audit_Native_Imports_And_Top_Level_Types_Once_Including_Shared_Handles()
    {
        var compat = CSharpEvidenceLoader.Load("Compat/sample.g.cs", FixtureLoader.Load("BindingGeneration/Oracle/ownership-compat.cs.txt"));
        var modern = CSharpEvidenceLoader.Load("Modern/sample.g.cs", FixtureLoader.Load("BindingGeneration/Oracle/ownership-modern.cs.txt"));
        var handles = CSharpEvidenceLoader.Load("Handles.g.cs", Handle);
        var evidence = CSharpEvidenceLoader.Combine(CSharpEvidenceLoader.Combine(compat.Evidence, modern.Evidence), handles.Evidence);

        var audit = OwnershipAudit.Run(evidence, ManifestFixture.RealCoreLibrary.BindingGeneration);

        await Assert.That(audit.Select(check => check.Name)).IsEquivalentTo(
            ["AlienClass", "AlienEnum", "AlienHandle", "AlienModernOnly", "AlienStruct", "Alien_Entry"], CollectionOrdering.Matching);
        await Assert.That(audit.Select(check => check.Message).Distinct(StringComparer.Ordinal))
            .IsEquivalentTo(["not covered by owned_prefixes"], CollectionOrdering.Matching);
        await Assert.That(audit.Errors).IsEmpty();
    }
    private const string Handle = "namespace SDL2; public readonly struct AlienHandle { } public readonly struct SDL_Handle { }";
}
