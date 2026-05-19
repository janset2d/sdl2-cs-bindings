using Build.Tests.Fixtures;
using System.Diagnostics;

namespace Build.Tests.Integration.BindingCompileCheck;

public sealed class BindingCompileCheckProjectTests
{
    [Test]
    public async Task Build_Should_Fail_When_GeneratedBindingInputs_Are_Empty()
    {
        using var generatedRoot = new TempDirectory("janset-empty-generated-bindings-");
        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "tests", "binding-compile-check", "SDL2.Core.CompileCheck.csproj");

        var result = await RunDotNetBuildAsync(
            repoRoot,
            projectPath,
            $"/p:GeneratedBindingsPreviewRoot={EnsureTrailingSeparator(generatedRoot.Path)}");

        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.CombinedOutput).Contains("BINDING-COMPILE-CHECK-001", StringComparison.Ordinal);
    }

    [Test]
    public async Task Build_Should_Reach_Compatibility_TargetFrameworks_By_Default()
    {
        using var generatedRoot = new TempDirectory("janset-generated-bindings-tfm-probe-");
        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "tests", "binding-compile-check", "SDL2.Core.CompileCheck.csproj");
        await File.WriteAllTextAsync(
            Path.Combine(generatedRoot.Path, "TfmProbe.g.cs"),
            """
            #if NETSTANDARD2_0
            #error BINDING-COMPILE-CHECK-PROBED-NETSTANDARD2_0
            #endif

            namespace SDL2;

            public static partial class TfmProbe
            {
            }
            """);

        var result = await RunDotNetBuildAsync(
            repoRoot,
            projectPath,
            $"/p:GeneratedBindingsPreviewRoot={EnsureTrailingSeparator(generatedRoot.Path)}");

        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.CombinedOutput).Contains("BINDING-COMPILE-CHECK-PROBED-NETSTANDARD2_0", StringComparison.Ordinal);
    }

    private static async Task<ProcessResult> RunDotNetBuildAsync(
        string workingDirectory,
        string projectPath,
        params string[] buildProperties)
    {
        using var intermediateRoot = new TempDirectory("janset-compile-check-obj-");
        using var outputRoot = new TempDirectory("janset-compile-check-bin-");
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        process.StartInfo.ArgumentList.Add("build");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("Release");
        process.StartInfo.ArgumentList.Add("--disable-build-servers");
        process.StartInfo.ArgumentList.Add("-p:UseSharedCompilation=false");
        process.StartInfo.ArgumentList.Add("-nodeReuse:false");
        process.StartInfo.ArgumentList.Add($"/p:BaseIntermediateOutputPath={EnsureTrailingSeparator(intermediateRoot.Path)}");
        process.StartInfo.ArgumentList.Add($"/p:BaseOutputPath={EnsureTrailingSeparator(outputRoot.Path)}");
        foreach (var property in buildProperties)
        {
            process.StartInfo.ArgumentList.Add(property);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start dotnet build.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);

        var combinedOutput = string.Concat(await standardOutput, await standardError);
        return new ProcessResult(process.ExitCode, combinedOutput);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var compileCheckProject = Path.Combine(
                directory.FullName,
                "tests",
                "binding-compile-check",
                "SDL2.Core.CompileCheck.csproj");

            if (File.Exists(compileCheckProject))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    private static string EnsureTrailingSeparator(string path) =>
        Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;

    private sealed record ProcessResult(int ExitCode, string CombinedOutput);
}
