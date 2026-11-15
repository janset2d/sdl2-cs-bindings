using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class RequiredSurfaceAuditTests
{
    [Test]
    public async Task Run_Should_Report_Equality_Per_Backend_Without_Comparing_Native_Annotations()
    {
        var source = FixtureLoader.Load("BindingGeneration/Oracle/required-audit.cs.txt");
        var audit = RequiredSurfaceAudit.Run(Config, Load(source), Load(source));
        await Assert.That(audit.Results.Select(row => $"{row.Symbol}|{row.Backend}|{row.Status}"))
            .IsEquivalentTo([
                "SDL_Init|Compat|present and equal", "SDL_Init|Modern|present and equal",
                "SDL_Literal|Compat|present and equal", "SDL_Literal|Modern|present and equal",
                "SDL_Negative|Compat|present and equal", "SDL_Negative|Modern|present and equal",
                "SDL_Computed|Compat|present and equal", "SDL_Computed|Modern|present and equal"
            ], CollectionOrdering.Matching);
        await Assert.That(audit.Results[0].Evidence).Contains("uint [NativeTypeName: Uint32]");
        await Assert.That(audit.Results[0].Evidence).Contains("parameters: (uint [NativeTypeName: Uint32])");
        await Assert.That(audit.Findings).IsEmpty();
    }

    private static SourceEvidence Load(string source) => CSharpEvidenceLoader.Load("sample.g.cs", source);

    [Test]
    public async Task Run_Should_Normalize_Managed_Type_Whitespace_And_Preserve_Parameter_Order()
    {
        var config = Config with
        {
            RequiredConstants = [],
            RequiredFunctions = [Config.RequiredFunctions[0] with
            {
                ReturnType = "uint *",
                Parameters = [new RequiredFunctionParameter { Name = "flags", Type = "uint *" }, new RequiredFunctionParameter { Name = "count", Type = "int" }]
            }]
        };
        var source = FixtureLoader.Load("BindingGeneration/Oracle/required-audit.cs.txt")
            .Replace("uint INTERNAL_Init", "uint* INTERNAL_Init", StringComparison.Ordinal)
            .Replace("uint flags", "uint* flags, int count", StringComparison.Ordinal);
        var reversed = source.Replace("uint* flags, int count", "int count, uint* flags", StringComparison.Ordinal);
        var audit = RequiredSurfaceAudit.Run(config, Load(source), Load(reversed));
        await Assert.That(audit.Results.Select(row => $"{row.Backend}|{row.Status}"))
            .IsEquivalentTo(["Compat|present and equal", "Modern|present and different"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Run_Should_Compare_Unsigned_Hex_As_A_Positive_Integer_Without_Suffixes()
    {
        var config = Config with { RequiredFunctions = [], RequiredConstants = [Config.RequiredConstants[0] with { Value = "255UL" }] };
        var source = FixtureLoader.Load("BindingGeneration/Oracle/required-audit.cs.txt").Replace("(+0x1UL)", "0xFFu", StringComparison.Ordinal);
        var audit = RequiredSurfaceAudit.Run(config, Load(source), Load(source));
        await Assert.That(audit.Results.Select(row => $"{row.Backend}|{row.Status}"))
            .IsEquivalentTo(["Compat|present and equal", "Modern|present and equal"], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("1.5f")]
    [Arguments("(-(+1.5f))")]
    public async Task Run_Should_Classify_Noninteger_Numeric_Initializers_As_Literal(string value)
    {
        var config = Config with
        {
            RequiredFunctions = [],
            RequiredConstants = [Config.RequiredConstants[0] with { Type = "float", Value = value }]
        };
        var source = FixtureLoader.Load("BindingGeneration/Oracle/required-audit.cs.txt")
            .Replace("const uint SDL_Literal", "const float SDL_Literal", StringComparison.Ordinal)
            .Replace("(+0x1UL)", value, StringComparison.Ordinal);
        var audit = RequiredSurfaceAudit.Run(config, Load(source), Load(source));
        await Assert.That(audit.Results.Select(row => $"{row.Backend}|{row.Status}"))
            .IsEquivalentTo(["Compat|present and equal", "Modern|present and equal"], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("return", true)]
    [Arguments("return", false)]
    [Arguments("parameter", true)]
    [Arguments("parameter", false)]
    [Arguments("arity", true)]
    [Arguments("arity", false)]
    [Arguments("literal value", true)]
    [Arguments("literal value", false)]
    [Arguments("computed value", true)]
    [Arguments("computed value", false)]
    public async Task Run_Should_Distinguish_Managed_Type_And_Value_Differences_On_Each_Backend(string difference, bool wrongCompat)
    {
        var source = FixtureLoader.Load("BindingGeneration/Oracle/required-audit.cs.txt");
        var wrong = difference switch
        {
            "return" => source.Replace("uint INTERNAL_Init", "int INTERNAL_Init", StringComparison.Ordinal),
            "parameter" => source.Replace("uint flags", "int flags", StringComparison.Ordinal),
            "arity" => source.Replace("uint flags", "uint flags, int extra", StringComparison.Ordinal),
            "literal value" => source.Replace("(+0x1UL)", "2u", StringComparison.Ordinal),
            "computed value" => source.Replace("1 << 2", "1 << 3", StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(difference))
        };
        var audit = RequiredSurfaceAudit.Run(Config, Load(wrongCompat ? wrong : source), Load(wrongCompat ? source : wrong));
        var symbol = difference switch { "literal value" => "SDL_Literal", "computed value" => "SDL_Computed", _ => "SDL_Init" };
        var backend = wrongCompat ? "Compat" : "Modern";
        string[] expected = wrongCompat
            ? ["Compat|present and different", "Modern|present and equal"]
            : ["Compat|present and equal", "Modern|present and different"];
        await Assert.That(audit.Results.Where(row => row.Symbol == symbol).Select(row => $"{row.Backend}|{row.Status}"))
            .IsEquivalentTo(expected, CollectionOrdering.Matching);
        await Assert.That(audit.Findings.Select(check => $"{check.Name}|{check.Message.Split(':')[0]}"))
            .IsEquivalentTo([$"{symbol}|{backend}"], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Run_Should_Distinguish_Not_Emitted_From_Missing_Backend_Evidence(bool missingCompat)
    {
        var empty = Load("namespace SDL2;");
        var missing = new SourceEvidence(SourceStatus.Missing, new CSharpEvidence([], [], []));
        var audit = RequiredSurfaceAudit.Run(Config, missingCompat ? missing : empty, missingCompat ? empty : missing);
        var compatStatus = missingCompat ? "Evidence Missing" : "not emitted";
        var modernStatus = missingCompat ? "not emitted" : "Evidence Missing";
        await Assert.That(audit.Results.Select(row => $"{row.Symbol}|{row.Backend}|{row.Status}"))
            .IsEquivalentTo([
                $"SDL_Init|Compat|{compatStatus}", $"SDL_Init|Modern|{modernStatus}",
                $"SDL_Literal|Compat|{compatStatus}", $"SDL_Literal|Modern|{modernStatus}",
                $"SDL_Negative|Compat|{compatStatus}", $"SDL_Negative|Modern|{modernStatus}",
                $"SDL_Computed|Compat|{compatStatus}", $"SDL_Computed|Modern|{modernStatus}"
            ], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Run_Should_Report_A_Missing_Initializer_As_Evidence_Missing(bool missingCompat)
    {
        var source = FixtureLoader.Load("BindingGeneration/Oracle/required-audit.cs.txt");
        var missing = source.Replace("const uint SDL_Literal = (+0x1UL)", "static readonly uint SDL_Literal", StringComparison.Ordinal);
        var audit = RequiredSurfaceAudit.Run(Config, Load(missingCompat ? missing : source), Load(missingCompat ? source : missing));
        string[] expected = missingCompat
            ? ["Compat|Evidence Missing", "Modern|present and equal"]
            : ["Compat|present and equal", "Modern|Evidence Missing"];
        await Assert.That(audit.Results.Where(row => row.Symbol == "SDL_Literal").Select(row => $"{row.Backend}|{row.Status}"))
            .IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("1 + 0", ConstantKind.Literal)]
    [Arguments("4", ConstantKind.Computed)]
    [Arguments("(uint)1", ConstantKind.Literal)]
    [Arguments("SDL_Negative", ConstantKind.Literal)]
    public async Task Run_Should_Compare_Constant_Kind_Independently_Of_Value(string value, ConstantKind expectedKind)
    {
        var config = Config with { RequiredFunctions = [], RequiredConstants = [Config.RequiredConstants[0] with { Value = value, Kind = expectedKind }] };
        var source = FixtureLoader.Load("BindingGeneration/Oracle/required-audit.cs.txt").Replace("(+0x1UL)", value, StringComparison.Ordinal);
        var audit = RequiredSurfaceAudit.Run(config, Load(source), Load(source));
        await Assert.That(audit.Results.Select(row => $"{row.Symbol}|{row.Backend}|{row.Status}"))
            .IsEquivalentTo(["SDL_Literal|Compat|present and different", "SDL_Literal|Modern|present and different"], CollectionOrdering.Matching);
        await Assert.That(audit.Findings.Select(check => check.Name))
            .IsEquivalentTo(["SDL_Literal", "SDL_Literal"], CollectionOrdering.Matching);
    }

    private static BindingGenerationConfig Config => ManifestFixture.RealCoreLibrary.BindingGeneration with
    {
        RequiredFunctions =
        [
            new RequiredFunctionConfig
            {
                Name = "SDL_Init", ReturnType = "uint", SourceHeader = "SDL.h",
                Parameters = [new RequiredFunctionParameter { Name = "flags", Type = "uint" }]
            }
        ],
        RequiredConstants =
        [
            new RequiredConstantConfig { Name = "SDL_Literal", Type = "uint", Value = "1", Kind = ConstantKind.Literal, SourceHeader = "SDL.h" },
            new RequiredConstantConfig { Name = "SDL_Negative", Type = "int", Value = "-1", Kind = ConstantKind.Literal, SourceHeader = "SDL.h" },
            new RequiredConstantConfig { Name = "SDL_Computed", Type = "uint", Value = "1   <<   2", Kind = ConstantKind.Computed, SourceHeader = "SDL.h" }
        ]
    };
}
