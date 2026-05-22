namespace Janset.Sdl2.AlimerSpike.Generator;

internal enum SpikeScope { Bootstrap, Full }

internal sealed record Sdl2SpikeOptions(
    string RepositoryRoot,
    string VcpkgTriplet,
    SpikeScope Scope,
    bool Emit,
    bool CleanOutput,
    string CoreScopeFile,
    string ImageScopeFile,
    string GeneratedRoot,
    string ReportsRoot)
{
    public string IncludeRoot => Path.Combine(RepositoryRoot, "vcpkg_installed", VcpkgTriplet, "include");
    public string Sdl2IncludeRoot => Path.Combine(IncludeRoot, "SDL2");

    public static Sdl2SpikeOptions FromArgs(string[] args)
    {
        var repositoryRoot = FindRepositoryRoot();
        var triplet = "x64-windows-hybrid";
        var scope = SpikeScope.Bootstrap;
        var emit = false;
        var cleanOutput = false;

        var index = 0;
        while (index < args.Length)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--vcpkg-triplet" when index + 1 < args.Length:
                    triplet = args[index + 1];
                    index += 2;
                    break;
                case "--scope" when index + 1 < args.Length:
                    scope = args[index + 1] switch
                    {
                        "bootstrap" => SpikeScope.Bootstrap,
                        "full" => SpikeScope.Full,
                        _ => throw new ArgumentException($"Unknown --scope value '{args[index + 1]}'. Use bootstrap or full."),
                    };
                    index += 2;
                    break;
                case "--emit":
                    emit = true;
                    index += 1;
                    break;
                case "--clean-output":
                    cleanOutput = true;
                    index += 1;
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown or incomplete argument '{argument}'. Supported: --vcpkg-triplet <triplet>, --scope bootstrap|full, --emit, --clean-output.",
                        nameof(args));
            }
        }

        var scopeRoot = Path.Combine(repositoryRoot, "spikes", "binding-generators", "scope");
        var coreScope = Path.Combine(scopeRoot, scope == SpikeScope.Bootstrap ? "bootstrap-sdl2-core.headers.txt" : "sdl2-core.headers.txt");
        var imageScope = Path.Combine(scopeRoot, scope == SpikeScope.Bootstrap ? "bootstrap-sdl2-image.headers.txt" : "sdl2-image.headers.txt");
        var generatedRoot = Path.Combine(repositoryRoot, "spikes", "binding-generators", "output", "alimer", "Generated");
        var reportsRoot = Path.Combine(repositoryRoot, "spikes", "binding-generators", "output", "reports");

        return new Sdl2SpikeOptions(
            repositoryRoot,
            triplet,
            scope,
            emit,
            cleanOutput,
            coreScope,
            imageScope,
            generatedRoot,
            reportsRoot);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found from the spike output directory.");
    }
}
