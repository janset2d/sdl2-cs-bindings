namespace Build.Tests.Fixtures;

internal static class ProgramCompositionRootData
{
    public const int ProbeExitCode = 37;
    public const string RepoRootPrefix = "janset-host-action-";
    public static string[] UserArguments(string repoRoot) => [CakeHostCommandLineData.RepoRootOptionName, repoRoot, "--target", "Info"];
    public static string[] ExpectedCakeArguments(string userRepoRoot, string cakeRepoRoot) =>
        [CakeHostCommandLineData.RepoRootOptionName, userRepoRoot, "--target", "Info", "--working", cakeRepoRoot];
}
