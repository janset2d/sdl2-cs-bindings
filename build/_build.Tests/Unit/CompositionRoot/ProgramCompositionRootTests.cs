using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using Build.Context;
using Build.Context.Options;
using Cake.Core.IO;

namespace Build.Tests.Unit.CompositionRoot;

public sealed class ProgramCompositionRootTests
{
    private static readonly Lock ParseRootLock = new();
    private static readonly Lock EnvironmentVariableLock = new();
    private static readonly RootCommand ParsingRoot = CreateParsingRoot();

    private static readonly Type ProgramType = typeof(BuildContext).Assembly.GetType("Program")
                                               ?? throw new InvalidOperationException("Unable to resolve Program type from Build assembly.");

    [Test]
    public async Task IsVerbosityArg_Should_Recognize_Long_And_Short_Flags()
    {
        var method = GetProgramHelper("g__IsVerbosityArg", typeof(string));

        var longOption = (bool)method.Invoke(null, ["--verbosity"])!;
        var shortOption = (bool)method.Invoke(null, ["-v"])!;
        var nonVerbosity = (bool)method.Invoke(null, ["--target"])!;

        await Assert.That(longOption).IsTrue();
        await Assert.That(shortOption).IsTrue();
        await Assert.That(nonVerbosity).IsFalse();
    }

    [Test]
    public async Task IsWorkingPathArg_Should_Recognize_Long_And_Short_Flags()
    {
        var method = GetProgramHelper("g__IsWorkingPathArg", typeof(string));

        var longOption = (bool)method.Invoke(null, ["--working"])!;
        var shortOption = (bool)method.Invoke(null, ["-w"])!;
        var nonWorking = (bool)method.Invoke(null, ["--target"])!;

        await Assert.That(longOption).IsTrue();
        await Assert.That(shortOption).IsTrue();
        await Assert.That(nonWorking).IsFalse();
    }

    [Test]
    public async Task GetEffectiveCakeArguments_Should_Inject_Working_Path_When_Not_Provided()
    {
        var method = GetProgramHelper(
            "g__GetEffectiveCakeArguments",
            typeof(string[]),
            typeof(DirectoryPath),
            typeof(InvocationContext));
        var originalArgs = Array.Empty<string>();
        var repoRoot = new DirectoryPath("C:/repo-root");
        var invocationContext = CreateInvocationContext(originalArgs);

        var effectiveArgs = RunWithActionsRunnerDebug(
            value: null,
            () => (string[])method.Invoke(null, [originalArgs, repoRoot, invocationContext])!);

        await Assert.That(effectiveArgs).Contains("--working");
        await Assert.That(effectiveArgs).Contains(repoRoot.FullPath);
        await Assert.That(effectiveArgs).DoesNotContain("--verbosity");
    }

    [Test]
    public async Task GetEffectiveCakeArguments_Should_Inject_Diagnostic_Verbosity_When_ActionsRunnerDebug_Is_True()
    {
        var method = GetProgramHelper(
            "g__GetEffectiveCakeArguments",
            typeof(string[]),
            typeof(DirectoryPath),
            typeof(InvocationContext));
        var originalArgs = Array.Empty<string>();
        var repoRoot = new DirectoryPath("C:/repo-root");
        var invocationContext = CreateInvocationContext(originalArgs);

        var effectiveArgs = RunWithActionsRunnerDebug(
            "true",
            () => (string[])method.Invoke(null, [originalArgs, repoRoot, invocationContext])!);

        await Assert.That(effectiveArgs).Contains("--verbosity");
        await Assert.That(effectiveArgs).Contains("diagnostic");
        await Assert.That(effectiveArgs).Contains("--working");
    }

    [Test]
    public async Task GetEffectiveCakeArguments_Should_Preserve_Explicit_Working_And_Verbosity()
    {
        var method = GetProgramHelper(
            "g__GetEffectiveCakeArguments",
            typeof(string[]),
            typeof(DirectoryPath),
            typeof(InvocationContext));
        var originalArgs = new[] { "--verbosity", "quiet", "--working", "C:/custom" };
        var repoRoot = new DirectoryPath("C:/repo-root");
        var invocationContext = CreateInvocationContext(originalArgs);

        var effectiveArgs = RunWithActionsRunnerDebug(
            "true",
            () => (string[])method.Invoke(null, [originalArgs, repoRoot, invocationContext])!);

        await Assert.That(effectiveArgs.Length).IsEqualTo(originalArgs.Length);
        for (var i = 0; i < originalArgs.Length; i++)
        {
            await Assert.That(effectiveArgs[i]).IsEqualTo(originalArgs[i]);
        }
    }

    [Test]
    public async Task DetermineRepoRootAsync_Should_Use_RepoRoot_Argument_When_Path_Exists()
    {
        var method = GetProgramHelper("g__DetermineRepoRootAsync", typeof(DirectoryInfo));
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "build-program-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        try
        {
            var task = (Task<DirectoryPath>)method.Invoke(null, [new DirectoryInfo(path)])!;
            var result = await task;

            await Assert.That(result.FullPath).IsEqualTo(new DirectoryPath(path).FullPath);
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    private static MethodInfo GetProgramHelper(string methodNameFragment, params Type[] parameterTypes)
    {
        var method = ProgramType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(m =>
                m.Name.Contains(methodNameFragment, StringComparison.Ordinal) &&
                HasParameterSignature(m, parameterTypes));

        return method ?? throw new InvalidOperationException($"Program helper containing '{methodNameFragment}' was not found.");
    }

    private static bool HasParameterSignature(MethodInfo method, params Type[] parameterTypes)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != parameterTypes.Length)
        {
            return false;
        }

        for (var i = 0; i < parameterTypes.Length; i++)
        {
            if (parameters[i].ParameterType != parameterTypes[i])
            {
                return false;
            }
        }

        return true;
    }

    private static InvocationContext CreateInvocationContext(params string[] args)
    {
        lock (ParseRootLock)
        {
            var parseResult = ParsingRoot.Parse(args);
            return new InvocationContext(parseResult);
        }
    }

    private static T RunWithActionsRunnerDebug<T>(string? value, Func<T> callback)
    {
        lock (EnvironmentVariableLock)
        {
            var previous = Environment.GetEnvironmentVariable("ACTIONS_RUNNER_DEBUG");
            try
            {
                Environment.SetEnvironmentVariable("ACTIONS_RUNNER_DEBUG", value);
                return callback();
            }
            finally
            {
                Environment.SetEnvironmentVariable("ACTIONS_RUNNER_DEBUG", previous);
            }
        }
    }

    private static RootCommand CreateParsingRoot()
    {
        var root = new RootCommand("Program helper parse root for tests");
        root.AddOption(CakeOptions.VerbosityOption);
        root.AddOption(CakeOptions.WorkingPathOption);
        return root;
    }
}
