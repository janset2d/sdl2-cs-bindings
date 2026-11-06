namespace Build.Tests.Fixtures.QualityGates;

internal static class SuppressionLedgerParserData
{
    public const string ConditionalImport = "<Project><!-- Frozen tooling imports root analyzers only after integration. -->" +
        "<Import Project=\"root.props\" Condition=\"'$(Frozen)' != 'true'\" /></Project>";
    public const string ExpectedConditionalImport = "build-switch z.props ['$(Frozen)' != 'true'] Import = root.props | " +
        "Frozen tooling imports root analyzers only after integration.";

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
