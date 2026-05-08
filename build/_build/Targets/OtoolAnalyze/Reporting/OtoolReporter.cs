using Build.Targets.OtoolAnalyze.Services;
using Cake.Core.IO;
using Spectre.Console;

namespace Build.Targets.OtoolAnalyze.Reporting;

public sealed class OtoolReporter(IAnsiConsole console)
{
    private readonly IAnsiConsole _console = console ?? throw new ArgumentNullException(nameof(console));

    public void WriteAnalysisHeader()
    {
        _console.Write(new FigletText("macOS Otool Analysis").Color(Color.Green));
        _console.WriteLine();
    }

    public void WriteModeHeader(string mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        _console.Write(new Rule($"[yellow]Analyzing {mode}[/]"));
    }

    public void WriteVcpkgFoundForTriplet(string triplet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);
        _console.MarkupLine($"[grey]Found vcpkg libraries for triplet:[/] [aqua]{triplet}[/]");
    }

    public void WriteVcpkgNotFoundWarning(IReadOnlyList<string> attemptedTriplets)
    {
        ArgumentNullException.ThrowIfNull(attemptedTriplets);
        _console.MarkupLine("[yellow]Vcpkg lib directory not found for any supported triplet[/]");
        foreach (var triplet in attemptedTriplets)
        {
            _console.MarkupLine($"[grey]  Try running:[/] ./external/vcpkg/vcpkg install sdl2:{triplet}");
        }
    }

    public void WriteNoDylibsWarning(DirectoryPath libDir)
    {
        ArgumentNullException.ThrowIfNull(libDir);
        _console.MarkupLine($"[yellow]No .dylib files found in:[/] {libDir.FullPath}");
    }

    public void WriteAnalysisSection(FilePath file, IReadOnlyList<ClassifiedDependency> deps)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(deps);

        _console.Write(new Rule($"[cyan]Analyzing: {file.GetFilename()}[/]"));

        var dependencyTable = new Table()
            .RoundedBorder()
            .BorderColor(Color.Blue)
            .AddColumn("[bold]Library Name[/]")
            .AddColumn("[bold]Full Path[/]")
            .AddColumn("[bold]Type[/]")
            .AddColumn("[bold]System Library?[/]");

        var systemLibs = new List<string>();
        var userLibs = new List<string>();
        var frameworkLibs = new List<string>();
        var rpathLibs = new List<string>();

        foreach (var dep in deps.OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            (dep.IsSystem ? systemLibs : userLibs).Add(dep.Name);
            switch (dep.Kind)
            {
                case LibraryKind.Framework: frameworkLibs.Add(dep.Name); break;
                case LibraryKind.RPath: rpathLibs.Add(dep.Name); break;
            }

            var typeColor = dep.Kind switch
            {
                LibraryKind.System => "green",
                LibraryKind.Framework => "blue",
                LibraryKind.RPath => "yellow",
                LibraryKind.User => "white",
                _ => "grey",
            };
            var systemColor = dep.IsSystem ? "green" : "red";

            dependencyTable.AddRow(
                $"[white]{dep.Name}[/]",
                $"[grey]{dep.Path}[/]",
                $"[{typeColor}]{dep.Kind}[/]",
                $"[{systemColor}]{(dep.IsSystem ? "Yes" : "No")}[/]");
        }

        _console.Write(dependencyTable);

        var statsGrid = new Grid().AddColumn().AddColumn();
        statsGrid.AddRow("[bold]Total Dependencies[/]", $"[white]{deps.Count}[/]");
        statsGrid.AddRow("[bold]System Libraries[/]", $"[green]{systemLibs.Count}[/]");
        statsGrid.AddRow("[bold]User Libraries[/]", $"[red]{userLibs.Count}[/]");
        statsGrid.AddRow("[bold]Frameworks[/]", $"[blue]{frameworkLibs.Count}[/]");
        statsGrid.AddRow("[bold]RPath Libraries[/]", $"[yellow]{rpathLibs.Count}[/]");

        _console.Write(new Panel(statsGrid)
            .Header($"[bold yellow]Analysis Summary: {file.GetFilename()}[/]", Justify.Left)
            .BorderColor(Color.Grey));

        if (systemLibs.Count > 0)
        {
            _console.Write(new Rule("[green]Suggested system_exclusions entries for manifest.json[/]"));
            var suggestions = new Table()
                .RoundedBorder()
                .BorderColor(Color.Green)
                .AddColumn("[bold]Library Name[/]")
                .AddColumn("[bold]Reason[/]");

            foreach (var sysLib in systemLibs.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
            {
                var reason = sysLib.Contains(".framework", StringComparison.Ordinal) ? "System Framework" : "System Library";
                suggestions.AddRow($"[green]{sysLib}[/]", $"[white]{reason}[/]");
            }

            _console.Write(suggestions);
        }

        _console.WriteLine();
    }
}
