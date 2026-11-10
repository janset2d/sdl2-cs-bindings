using System.CommandLine;
using System.CommandLine.Help;
using System.Reflection;
using Build.Host.Cli;
using Build.Host.Cli.Options;
using Build.Tests.Fixtures;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Host.Cli;

public sealed class CakeHostCommandLineTests
{
    [Test]
    public async Task BindParsedArguments_Should_Map_Every_Option_When_All_Are_Given()
    {
        var root = CakeHostCommandLine.CreateRootCommand((_, _) => Task.FromResult(0));
        var parseResult = root.Parse(CakeHostCommandLineData.AllArguments);

        var result = CakeHostCommandLine.BindParsedArguments(parseResult);

        await Assert.That(parseResult.Errors).IsEmpty();
        await Assert.That(result.RepoRoot?.FullName).IsEqualTo(CakeHostCommandLineData.RepoRoot);
        await Assert.That(result.Config).IsEqualTo(CakeHostCommandLineData.Config);
        await Assert.That(result.VcpkgDir?.FullName).IsEqualTo(CakeHostCommandLineData.VcpkgDir);
        await Assert.That(result.VcpkgInstalledDir?.FullName).IsEqualTo(CakeHostCommandLineData.VcpkgInstalledDir);
        await Assert.That(result.Library.ToArray()).IsEquivalentTo(CakeHostCommandLineData.Libraries, CollectionOrdering.Matching);
        await Assert.That(result.Rid).IsEqualTo(CakeHostCommandLineData.Rid);
        await Assert.That(result.Dll.ToArray()).IsEquivalentTo(CakeHostCommandLineData.Dlls, CollectionOrdering.Matching);
        await Assert.That(result.Suffix).IsEqualTo(CakeHostCommandLineData.Suffix);
        await Assert.That(result.Scope.ToArray()).IsEquivalentTo(CakeHostCommandLineData.Scope, CollectionOrdering.Matching);
        await Assert.That(result.ExplicitVersion.ToArray()).IsEquivalentTo(CakeHostCommandLineData.ExplicitVersion, CollectionOrdering.Matching);
        await Assert.That(result.ExplicitVersions).IsEqualTo(CakeHostCommandLineData.ExplicitVersions);
        await Assert.That(result.VersionsFile).IsEqualTo(CakeHostCommandLineData.VersionsFile);
    }

    [Test]
    public async Task BindParsedArguments_Should_Return_Empty_Lists_And_Defaults_When_Options_Are_Absent()
    {
        var root = CakeHostCommandLine.CreateRootCommand((_, _) => Task.FromResult(0));

        var result = CakeHostCommandLine.BindParsedArguments(root.Parse([]));

        await Assert.That(result.Config).IsEqualTo(CakeHostCommandLineData.DefaultConfig);
        await Assert.That(result.RepoRoot).IsNull();
        await Assert.That(result.VcpkgDir).IsNull();
        await Assert.That(result.VcpkgInstalledDir).IsNull();
        await Assert.That(result.Library).IsEmpty();
        await Assert.That(result.Rid).IsNull();
        await Assert.That(result.Dll).IsEmpty();
        await Assert.That(result.Suffix).IsNull();
        await Assert.That(result.Scope).IsEmpty();
        await Assert.That(result.ExplicitVersion).IsEmpty();
        await Assert.That(result.ExplicitVersions).IsNull();
        await Assert.That(result.VersionsFile).IsNull();
    }

    [Test]
    public async Task CliOptions_Should_Have_A_Description_And_The_Recorded_Aliases()
    {
        var options = typeof(CakeOptions).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(CakeOptions).Namespace)
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => typeof(Option).IsAssignableFrom(field.FieldType))
            .Select(field => (Option)field.GetValue(null)!)
            .OrderBy(option => option.Name, StringComparer.Ordinal)
            .ToArray();
        var root = CakeHostCommandLine.CreateRootCommand((_, _) => Task.FromResult(0));
        var registered = root.Options
            .Where(option => option is not HelpOption and not VersionOption)
            .OrderBy(option => option.Name, StringComparer.Ordinal)
            .ToArray();

        await Assert.That(registered.Length).IsEqualTo(CakeHostCommandLineData.RepositoryOptionCount);
        await Assert.That(options).IsEquivalentTo(registered, CollectionOrdering.Matching);

        foreach (var option in options)
        {
            await Assert.That(option.Description).IsNotNullOrWhiteSpace();

            await Assert.That(option.Aliases.Order(StringComparer.Ordinal).ToArray())
                .IsEquivalentTo(CakeHostCommandLineData.RecordedAliases[option.Name], CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task ParseResult_Should_Expose_Only_User_Tokens()
    {
        var root = CakeHostCommandLine.CreateRootCommand((_, _) => Task.FromResult(0));

        var result = root.Parse(CakeHostCommandLineData.UserTokens);

        await Assert.That(root.Options.Select(option => option.Name).ToArray()).IsEquivalentTo(CakeHostCommandLineData.OptionNames, CollectionOrdering.Matching);
        var forwarded = ProgramCompositionRootFixture.RunWithActionsRunnerDebug(
            null, () => EntryPoint.GetEffectiveCakeArguments(new DirectoryPath(CakeHostCommandLineData.RepoRoot), result));
        await Assert.That(forwarded).IsEquivalentTo(CakeHostCommandLineData.ForwardedUserTokens, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(CakeHostCommandLineData), nameof(CakeHostCommandLineData.LowercaseVerbosity))]
    public async Task VerbosityOption_Should_Parse_Lowercase_Cake_Verbosity(string value, Verbosity expected)
    {
        var root = CakeHostCommandLine.CreateRootCommand((_, _) => Task.FromResult(0));

        var result = root.Parse(["-v", value]);

        await Assert.That(result.Errors).IsEmpty();
        await Assert.That(result.GetValue(CakeOptions.VerbosityOption)).IsEqualTo(expected);
    }

    [Test]
    public async Task VerbosityOption_Should_Report_An_Error_When_The_Value_Is_Not_A_Cake_Verbosity()
    {
        var root = CakeHostCommandLine.CreateRootCommand((_, _) => Task.FromResult(0));

        var result = root.Parse(CakeHostCommandLineData.InvalidVerbosityArguments);

        await Assert.That(result.Errors.Select(error => error.Message).ToArray())
            .IsEquivalentTo(CakeHostCommandLineData.InvalidVerbosityErrors, CollectionOrdering.Matching);
    }

    [Test]
    public async Task HelpToken_Should_Route_As_Recorded_Before_The_Port()
    {
        var forwardedTokens = Array.Empty<string>();
        var root = CakeHostCommandLine.CreateRootCommand((result, _) =>
        {
            forwardedTokens = [.. result.Tokens.Select(token => token.Value)];
            return Task.FromResult(CakeHostCommandLineData.CancelledProbeExitCode);
        });
        using var output = new StringWriter();
        var configuration = new InvocationConfiguration { Output = output };

        var cakeHelpExit = await root.Parse(CakeHostCommandLineData.CakeHelp).InvokeAsync(configuration);
        await Assert.That(cakeHelpExit).IsEqualTo(CakeHostCommandLineData.CancelledProbeExitCode);
        await Assert.That(forwardedTokens).IsEquivalentTo(CakeHostCommandLineData.CakeHelp, CollectionOrdering.Matching);

        foreach (var alias in CakeHostCommandLineData.WrapperHelpAliases)
        {
            output.GetStringBuilder().Clear();
            var wrapperHelpExit = await root.Parse([alias]).InvokeAsync(configuration);
            await Assert.That(wrapperHelpExit).IsEqualTo(0);
            await Assert.That(output.ToString()).Contains(CakeHostCommandLineData.RootDescription);
            await Assert.That(output.ToString()).Contains("--repo-root");
        }
    }

    [Test]
    public async Task InvokeAsync_Should_Cancel_The_Action_Token_When_The_Invocation_Is_Cancelled()
    {
        using var probe = new CakeHostCancellationProbe();
        var root = CakeHostCommandLine.CreateRootCommand((_, token) => probe.InvokeAsync(token));
        using var cancellation = new CancellationTokenSource();
        var invocation = root.Parse([]).InvokeAsync(configuration: null, cancellation.Token);

        try
        {
            var started = probe.WaitForStartAsync();
            var first = await Task.WhenAny(started, invocation);
            await Assert.That(first).IsSameReferenceAs(started);
            await cancellation.CancelAsync();
        }
        finally
        {
            probe.Finish();
        }

        var exitCode = await invocation;
        await Assert.That(probe.ObservedCancellation).IsTrue();
        await Assert.That(probe.CleanupRan).IsTrue();
        await Assert.That(exitCode).IsEqualTo(CakeHostCommandLineData.CancelledProbeExitCode);
    }
}
