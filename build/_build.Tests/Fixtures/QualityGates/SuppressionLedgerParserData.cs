namespace Build.Tests.Fixtures.QualityGates;

internal static class SuppressionLedgerParserData
{
    public const string ConditionalImport = "<Project><!-- Frozen tooling imports root analyzers only after integration. -->" +
        "<Import Project=\"root.props\" Condition=\"'$(Frozen)' != 'true'\" /></Project>";
    public const string ExpectedConditionalImport = "build-switch z.props ['$(Frozen)' != 'true'] Import = root.props | " +
        "Frozen tooling imports root analyzers only after integration.";

    public static IEnumerable<(string Content, string Expected)> ConditionalImports() =>
    [
        (ConditionalImport, ExpectedConditionalImport),
        ("""
            <Project>
              <!-- Postprocess and the oracle keep their own build settings until they move into build/_build. -->
              <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" Condition=" '$(MSBuildProjectName)' != 'Janset.SDL2.PostProcess' And '$(MSBuildProjectName)' != 'oracle.cs' " />
            </Project>
            """,
            "build-switch z.props ['$(MSBuildProjectName)' != 'Janset.SDL2.PostProcess' And '$(MSBuildProjectName)' != 'oracle.cs'] " +
            "Import = $([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../')) | " +
            "Postprocess and the oracle keep their own build settings until they move into build/_build."),
        ("<Project><Import Project=\"root.props\" Condition=\"  '$(Frozen)'  !=  'true'  \" /></Project>",
            "build-switch z.props ['$(Frozen)' != 'true'] Import = root.props"),
    ];

    public static IReadOnlyList<(string Path, string Content)> HeaderAndConditions =>
    [
        ("tool.cs", "#!/usr/bin/env dotnet\n// header\n\n#:property RunAnalyzers=false\nclass Z {}\n#:property NoWarn=CA1000\n"),
        ("z.props", "<Project><PropertyGroup Condition=\"  '$(A)'  ==  'true' \" >" +
            "<RunAnalyzers Condition=\" '$(B)' !=  'false'  \">false</RunAnalyzers></PropertyGroup></Project>"),
    ];

    public static IReadOnlyList<string> ExpectedHeaderAndConditions =>
    [
        "build-switch tool.cs [file-based header] RunAnalyzers = false",
        "build-switch z.props ['$(A)' == 'true' and '$(B)' != 'false'] RunAnalyzers = false",
    ];

    public static IEnumerable<(string Content, string Expected)> ConfigEntries() => [.. LowSeverityConfigs(), .. ConfigReasons(), .. GlobalConfigPreambles()];

    public static IEnumerable<(string Content, string Expected)> GlobalConfigPreambles() =>
    [
        ("[a.cs]\nis_global = true\ndotnet_diagnostic.CA1000.severity = none\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = none"),
        ("# is_global is not a property here\n[a.cs]\ndotnet_diagnostic.CA1000.severity = none\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = none | # is_global is not a property here"),
        ("dotnet_diagnostic.CA1000.severity = none\nIS_GLOBAL = false\n",
            "globalconfig .editorconfig [] dotnet_diagnostic.CA1000.severity = none"),
    ];

    public static IReadOnlyList<(string Path, string Content)> IgnoreTrivia =>
    [
        ("z.cs", "/// SLOPWATCH-IGNORE: sw002 documentation reason\nclass Z {}\n" +
            "/** slopwatch-ignore: SW004 block reason */\nclass A {}\n" +
            "// SlopWatch-Ignore: SW003 plain reason\n"),
    ];

    public static IReadOnlyList<string> ExpectedIgnoreEntries =>
    [
        "slopwatch-ignore z.cs [] SW002 | documentation reason",
        "slopwatch-ignore z.cs [] SW003 | plain reason",
        "slopwatch-ignore z.cs [] SW004 | block reason",
    ];

    public static IReadOnlyList<(string Path, string Content)> SlopwatchFiles =>
    [
        (".slopwatch/slopwatch.json", "{\"rules\":{\"SW002\":{\"severity\":\"info\"},\"SW001\":{\"enabled\":false,\"severity\":\"error\"}}," +
            "\"minSeverity\":\"warning\",\"exclude\":\"z/**,a/**\"}"),
        (".slopwatch/config.json.example", "example, not active configuration"),
    ];

    public static IReadOnlyList<string> ExpectedSlopwatchEntries =>
    [
        "slopwatch .slopwatch/slopwatch.json [SW001] enabled = false",
        "slopwatch .slopwatch/slopwatch.json [SW001] severity = error",
        "slopwatch .slopwatch/slopwatch.json [SW002] severity = info",
        "slopwatch .slopwatch/slopwatch.json [] exclude = a/**",
        "slopwatch .slopwatch/slopwatch.json [] exclude = z/**",
        "slopwatch .slopwatch/slopwatch.json [] minSeverity = warning",
    ];

    public static IEnumerable<(string Content, string Expected)> UnknownSlopwatchProperties() =>
    [
        ("{\"suppressions\":[]}", ".slopwatch/slopwatch.json: unknown root property 'suppressions' is not an inventoried suppression channel."),
        ("{\"globalSuppressions\":[]}", ".slopwatch/slopwatch.json: unknown root property 'globalSuppressions' is not an inventoried suppression channel."),
        ("{\"custom\":true}", ".slopwatch/slopwatch.json: unknown root property 'custom' is not an inventoried suppression channel."),
        ("{\"rules\":{\"SW002\":{\"custom\":true}}}", ".slopwatch/slopwatch.json: unknown SW002 property 'custom' is not an inventoried suppression channel."),
    ];

    public const string NamespaceScopedAlias = "namespace N { using Ignore = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute; " +
        "[Ignore(\"Design\", \"CA1000\")] class Z {} }";
    public const string ExpectedNamespaceScopedAlias = "suppress-message z.cs [] CA1000";

    public static IEnumerable<(string Content, string Expected)> ConfigReasons() =>
    [
        ("# unrelated\n\n[a.cs]\ndotnet_diagnostic.CA1000.severity = none\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = none"),
        ("# SECTION 4: switches\n[a.cs]\ndotnet_diagnostic.CA1000.severity = none\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = none"),
        ("# SECTION 4: switches\n# section reason\n[a.cs]\ndotnet_diagnostic.CA1000.severity = none\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = none | # section reason"),
        ("# section reason\n[a.cs]\n# key reason\ndotnet_diagnostic.CA1000.severity = none\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = none | # key reason"),
        ("# section reason\n[a.cs]\n\ndotnet_diagnostic.CA1000.severity = none\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = none"),
        ("# FILE-SCOPED ARBITRATION\n[a.cs]\ndotnet_diagnostic.CA1000.severity = none\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = none"),
        ("# REPOSITORY ANALYZERS\n# section reason\n[a.cs]\ndotnet_diagnostic.CA1000.severity = none\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = none | # section reason"),
    ];

    public const string MultiRuleConfig = "# shared reason\n# Winner: first policy; CA1000 remains enabled elsewhere.\n" +
        "# Winner: second policy; CA1001 remains enabled elsewhere.\n[a.cs]\n" +
        "dotnet_diagnostic.CA1001.severity = none\ndotnet_diagnostic.CA1000.severity = none\n";

    public static IReadOnlyList<string> ExpectedMultiRuleEntries =>
    [
        "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = none | # shared reason # Winner: first policy; CA1000 remains enabled elsewhere.",
        "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1001.severity = none | # shared reason # Winner: second policy; CA1001 remains enabled elsewhere.",
    ];

    public static IEnumerable<string> BroadSuppressions() =>
    [
        "[assembly: SuppressMessage(\"Design\", \"CA1000\")]",
        "[module: SuppressMessage(\"Design\", \"CA1000\")]",
    ];

    public const string BroadSuppressionError = "z.cs:1: [SuppressMessage(\"Design\", \"CA1000\")] " +
        "is an assembly- or module-level suppression; the ledger reads only suppressions on the code they sit on.";

    public static IEnumerable<(string Content, string Expected)> LowSeverityConfigs() =>
    [
        ("# reason\n[a.cs] # section\ndotnet_diagnostic.CA1000.severity = none # note\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = none | # reason"),
        ("[a.cs] ; section\ndotnet_diagnostic.CA1000.severity = silent ; note\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = silent"),
        ("[a.cs]\ndotnet_diagnostic.CA1000.severity = refactoring\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = refactoring"),
        ("[a.cs]\ndotnet_diagnostic.CA1000.severity = suggestion\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1000.severity = suggestion"),
        ("[a.cs]\ncsharp_style_var_elsewhere = true:none # note\n",
            "editorconfig .editorconfig [a.cs] csharp_style_var_elsewhere = true:none"),
        ("[a.cs]\ncsharp_style_var_elsewhere = true:silent\n",
            "editorconfig .editorconfig [a.cs] csharp_style_var_elsewhere = true:silent"),
        ("[a.cs]\ncsharp_style_var_elsewhere = true:refactoring ; note\n",
            "editorconfig .editorconfig [a.cs] csharp_style_var_elsewhere = true:refactoring"),
        ("[a.cs]\ncsharp_style_var_elsewhere = true:suggestion\n",
            "editorconfig .editorconfig [a.cs] csharp_style_var_elsewhere = true:suggestion"),
        ("[a.cs]\nGENERATED_CODE = TRUE\n", "editorconfig .editorconfig [a.cs] GENERATED_CODE = TRUE"),
        ("[a.cs]\nROSLYNATOR_ANALYZERS.ENABLED_BY_DEFAULT = FALSE\n",
            "editorconfig .editorconfig [a.cs] ROSLYNATOR_ANALYZERS.ENABLED_BY_DEFAULT = FALSE"),
        ("[a.cs]\nDOTNET_CODE_QUALITY.CA1000.api_surface = public\n",
            "editorconfig .editorconfig [a.cs] DOTNET_CODE_QUALITY.CA1000.api_surface = public"),
        ("[a.cs]\nDOTNET_DIAGNOSTIC.CA1000.SEVERITY = NoNe\n",
            "editorconfig .editorconfig [a.cs] DOTNET_DIAGNOSTIC.CA1000.SEVERITY = NoNe"),
        ("[a.cs]\nCSHARP_STYLE_VAR_ELSEWHERE = true:SuGgEsTiOn\n",
            "editorconfig .editorconfig [a.cs] CSHARP_STYLE_VAR_ELSEWHERE = true:SuGgEsTiOn"),
        ("[a.cs]\nca1000.api_surface = public\n", "editorconfig .editorconfig [a.cs] ca1000.api_surface = public"),
        ("[a.cs]\ndotnet_diagnostic.CA1000.severity = warning\ndotnet_diagnostic.CA1001.severity = error\n" +
            "csharp_style_var_elsewhere = true:warning\ncsharp_style_var_for_built_in_types = true:error\n" +
            "generated_code = false\nroslynator_analyzers.enabled_by_default = true\nunrelated_option = false\n" +
            "dotnet_diagnostic.CA1002.severity = none\n",
            "editorconfig .editorconfig [a.cs] dotnet_diagnostic.CA1002.severity = none"),
    ];

    public static IEnumerable<(string Path, string Content, string Expected)> Refusals() =>
    [
        ("z.ruleset", "<RuleSet />", "z.ruleset: rule sets are not an inventoried suppression channel."),
        ("z.cs", "[SuppressMessage(\"Design\", Rule)] class Z {}",
            "z.cs: suppression rule must be a string literal: SuppressMessage(\"Design\", Rule)"),
        ("z.csproj", "<Project><PropertyGroup><NoWarn>$(OtherWarnings)</NoWarn></PropertyGroup></Project>",
            "z.csproj: NoWarn takes hidden IDs from $(OtherWarnings)."),
    ];

    public static IEnumerable<(string Content, string Expected)> InvalidConfigs() =>
    [
        ("[a.cs]\nnot a property\n", ".editorconfig:2 is not a section, a property or a comment: 'not a property'."),
        ("[a.cs] invalid\n", ".editorconfig:1 is not a section, a property or a comment: '[a.cs] invalid'."),
    ];

    public static IEnumerable<(string Content, string Expected)> HiddenProjectWarnings() =>
    [
        ("<Project><ItemGroup><Compile><NoWarn>CA1000</NoWarn></Compile></ItemGroup></Project>",
            "z.csproj: <NoWarn> outside a PropertyGroup (item metadata) is a warning list the suppression ledger does not read; set it as a property."),
        ("<Project><ItemGroup><Compile><NoError>CA1000</NoError></Compile></ItemGroup></Project>",
            "z.csproj: <NoError> outside a PropertyGroup (item metadata) is a warning list the suppression ledger does not read; set it as a property."),
        ("<Project><ItemGroup><Compile><WarningsNotAsErrors>CA1000</WarningsNotAsErrors></Compile></ItemGroup></Project>",
            "z.csproj: <WarningsNotAsErrors> outside a PropertyGroup (item metadata) is a warning list the suppression ledger does not read; set it as a property."),
        ("<Project><ItemGroup><Compile><MSBuildWarningsAsMessages>CA1000</MSBuildWarningsAsMessages></Compile></ItemGroup></Project>",
            "z.csproj: <MSBuildWarningsAsMessages> outside a PropertyGroup (item metadata) is a warning list the suppression ledger does not read; set it as a property."),
        ("<Project><ItemGroup><Compile NoWarn=\"CA1000\" /></ItemGroup></Project>",
            "z.csproj: <Compile NoWarn=\"…\"> is a warning list on an item or a task, which the suppression ledger does not read; set NoWarn as a property."),
        ("<Project><ItemGroup><Compile NoError=\"CA1000\" /></ItemGroup></Project>",
            "z.csproj: <Compile NoError=\"…\"> is a warning list on an item or a task, which the suppression ledger does not read; set NoWarn as a property."),
        ("<Project><Target Name=\"Compile\"><Csc WarningsNotAsErrors=\"CA1000\" /></Target></Project>",
            "z.csproj: <Csc WarningsNotAsErrors=\"…\"> is a warning list on an item or a task, which the suppression ledger does not read; set NoWarn as a property."),
        ("<Project><Target Name=\"Compile\"><Csc MSBuildWarningsAsMessages=\"CA1000\" /></Target></Project>",
            "z.csproj: <Csc MSBuildWarningsAsMessages=\"…\"> is a warning list on an item or a task, which the suppression ledger does not read; set NoWarn as a property."),
        ("<Project><Target Name=\"Compile\"><Csc DisabledWarnings=\"CA1000\" /></Target></Project>",
            "z.csproj: <Csc DisabledWarnings=\"…\"> is a warning list on an item or a task, which the suppression ledger does not read; set NoWarn as a property."),
    ];
}
