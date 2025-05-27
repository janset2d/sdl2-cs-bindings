#pragma warning disable S2737, CA1031

using Build.Context;
using Build.Tools.Otool;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core.IO;
using Cake.Frosting;
using Spectre.Console;

namespace Build.Tasks.Dependency;

[TaskName("Otool-Analyze")]
public class OtoolAnalyzeTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        AnsiConsole.Write(new FigletText("macOS Otool Analysis").Color(Color.Green));
        AnsiConsole.WriteLine();

        // Analyze specific libraries if provided
        if (context.DumpbinConfiguration.DllToDump.Any())
        {
            await AnalyzeSpecificLibrariesAsync(context);
        }
        else
        {
            await AnalyzeVcpkgLibrariesAsync(context);
        }
    }

    private static async Task AnalyzeSpecificLibrariesAsync(BuildContext context)
    {
        AnsiConsole.Write(new Rule("[yellow]Analyzing Specific Libraries[/]").RuleStyle("grey"));

        foreach (var libraryPath in context.DumpbinConfiguration.DllToDump)
        {
            var file = context.File(libraryPath);

            if (!context.FileExists(file))
            {
                context.Warning("File not found: {0}", file.Path);
                continue;
            }

            await AnalyzeSingleLibraryAsync(context, file);
        }
    }

    private static async Task AnalyzeVcpkgLibrariesAsync(BuildContext context)
    {
        AnsiConsole.Write(new Rule("[yellow]Analyzing Vcpkg Libraries[/]").RuleStyle("grey"));

        // Look for vcpkg installed libraries - try both x64 and arm64
        var possibleTriplets = new[] { "x64-osx-dynamic", "arm64-osx-dynamic" };
        DirectoryPath? vcpkgLibDir = null;

        foreach (var triplet in possibleTriplets)
        {
            var testDir = context.Paths.GetVcpkgInstalledLibDir(triplet);
            if (!context.DirectoryExists(testDir))
            {
                continue;
            }

            vcpkgLibDir = testDir;
            context.Information("Found vcpkg libraries for triplet: {0}", triplet);
            break;
        }

        if (vcpkgLibDir == null || !context.DirectoryExists(vcpkgLibDir))
        {
            context.Warning("Vcpkg lib directory not found for any supported triplet");
            context.Information("Try running: ./external/vcpkg/vcpkg install sdl2:x64-osx-dynamic");
            context.Information("Or: ./external/vcpkg/vcpkg install sdl2:arm64-osx-dynamic");
            return;
        }

        var dylibFiles = context.GetFiles($"{vcpkgLibDir}/*.dylib").ToList();

        if (dylibFiles.Count == 0)
        {
            context.Warning("No .dylib files found in: {0}", vcpkgLibDir);
            return;
        }

        context.Information("Found {0} dylib file(s) to analyze", dylibFiles.Count);

        foreach (var dylibFile in dylibFiles)
        {
            await AnalyzeSingleLibraryAsync(context, dylibFile);
        }
    }

#pragma warning disable MA0051
    private static async Task AnalyzeSingleLibraryAsync(BuildContext context, FilePath file)
#pragma warning restore MA0051
    {
        AnsiConsole.Write(new Rule($"[cyan]Analyzing: {file.GetFilename()}[/]").RuleStyle("blue"));

        try
        {
            var settings = new OtoolSettings(file);
            var dependencies = await Task.Run(() => context.OtoolDependencies(settings));

            // Create analysis tables
            var dependencyTable = new Table()
                .RoundedBorder()
                .BorderColor(Color.Blue)
                .AddColumn("[bold]Library Name[/]")
                .AddColumn("[bold]Full Path[/]")
                .AddColumn("[bold]Type[/]")
                .AddColumn("[bold]System Library?[/]");

            var systemLibraries = new List<string>();
            var userLibraries = new List<string>();
            var frameworkLibraries = new List<string>();
            var rpathLibraries = new List<string>();

            foreach (var (libraryName, fullPath) in dependencies.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
            {
                var (libraryType, isSystemLibrary) = CategorizeLibrary(libraryName, fullPath);

                if (isSystemLibrary)
                {
                    systemLibraries.Add(libraryName);
                }
                else
                {
                    userLibraries.Add(libraryName);
                }

                switch (libraryType)
                {
                    case "Framework":
                        frameworkLibraries.Add(libraryName);
                        break;
                    case "RPath":
                        rpathLibraries.Add(libraryName);
                        break;
                }

                var typeColor = libraryType switch
                {
                    "System" => "green",
                    "Framework" => "blue",
                    "RPath" => "yellow",
                    "User" => "white",
                    _ => "grey"
                };

                var systemColor = isSystemLibrary ? "green" : "red";

                dependencyTable.AddRow(
                    $"[white]{libraryName}[/]",
                    $"[grey]{fullPath}[/]",
                    $"[{typeColor}]{libraryType}[/]",
                    $"[{systemColor}]{(isSystemLibrary ? "Yes" : "No")}[/]");
            }

            AnsiConsole.Write(dependencyTable);

            // Summary statistics
            var statsGrid = new Grid()
                .AddColumn()
                .AddColumn();

            statsGrid.AddRow("[bold]Total Dependencies[/]", $"[white]{dependencies.Count}[/]");
            statsGrid.AddRow("[bold]System Libraries[/]", $"[green]{systemLibraries.Count}[/]");
            statsGrid.AddRow("[bold]User Libraries[/]", $"[red]{userLibraries.Count}[/]");
            statsGrid.AddRow("[bold]Frameworks[/]", $"[blue]{frameworkLibraries.Count}[/]");
            statsGrid.AddRow("[bold]RPath Libraries[/]", $"[yellow]{rpathLibraries.Count}[/]");

            var statsPanel = new Panel(statsGrid)
                .Header($"[bold yellow]Analysis Summary: {file.GetFilename()}[/]", Justify.Left)
                .BorderColor(Color.Grey);

            AnsiConsole.Write(statsPanel);

            // Suggest additions to system_artefacts.json
            if (systemLibraries.Count != 0)
            {
                AnsiConsole.Write(new Rule("[green]Suggested system_artefacts.json entries[/]").RuleStyle("green"));

                var suggestions = new Table()
                    .RoundedBorder()
                    .BorderColor(Color.Green)
                    .AddColumn("[bold]Library Name[/]")
                    .AddColumn("[bold]Reason[/]");

                foreach (var sysLib in systemLibraries.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
                {
                    var reason = sysLib.Contains(".framework", StringComparison.Ordinal)
                        ? "System Framework"
                        : "System Library";
                    suggestions.AddRow($"[green]{sysLib}[/]", $"[white]{reason}[/]");
                }

                AnsiConsole.Write(suggestions);
            }

            context.Information("Analysis complete for {0}: {1} total dependencies ({2} system, {3} user)",
                file.GetFilename(), dependencies.Count, systemLibraries.Count, userLibraries.Count);
        }
        catch (Exception ex)
        {
            context.Error("Failed to analyze {0}: {1}", file.GetFilename(), ex.Message);
        }

        AnsiConsole.WriteLine();
    }

    private static (string Type, bool IsSystemLibrary) CategorizeLibrary(string libraryName, string fullPath)
    {
        // Categorize based on path patterns
        if (fullPath.StartsWith("/usr/lib/", StringComparison.Ordinal) || fullPath.StartsWith("/System/Library/", StringComparison.Ordinal))
        {
            return libraryName.Contains(".framework", StringComparison.Ordinal)
                ? ("Framework", true)
                : ("System", true);
        }

        if (fullPath.StartsWith("@rpath/", StringComparison.Ordinal)
            || fullPath.StartsWith("@loader_path/", StringComparison.Ordinal)
            || fullPath.StartsWith("@executable_path/", StringComparison.Ordinal))
        {
            return ("RPath", false);
        }

        if (libraryName.Contains(".framework", StringComparison.Ordinal))
        {
            return ("Framework", fullPath.Contains("/System/", StringComparison.Ordinal));
        }

        // Additional heuristics for system libraries
        var commonSystemLibraries = new[]
        {
            "libSystem.B.dylib", "libc++.1.dylib", "libobjc.A.dylib", "libz.1.dylib",
            "CoreFoundation.framework", "Foundation.framework", "AppKit.framework",
            "Cocoa.framework", "Carbon.framework", "IOKit.framework",
        };

        return !commonSystemLibraries.Contains(libraryName, StringComparer.Ordinal)
            ? ("User", false)
            : ("System", true);
    }
}
