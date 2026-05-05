#pragma warning disable CA1031

using System.ComponentModel;
using System.Runtime.InteropServices;
using Build.Host;
using Cake.Common;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Spectre.Console;

namespace Build.Targets.Info;

[TaskName("Info")]
public sealed class InfoTask : AsyncFrostingTask<BuildContext>
{
    private readonly IAnsiConsole _console;

    public InfoTask(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _console.Write(new FigletText("Build Info").Color(Color.CornflowerBlue));
        _console.WriteLine();

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn();

        void AddRow(string key, string value) => grid.AddRow($"[bold aqua]{key}:[/]", value);

        AddRow("Operating System", $"{context.Environment.Platform.Family}");
        AddRow("OS Version", $"{Environment.OSVersion}");
        AddRow("OS Architecture", $"{RuntimeInformation.OSArchitecture}");
        AddRow("Is 64-bit OS", $"[{(context.Environment.Platform.Is64Bit ? "green" : "red")}]{context.Environment.Platform.Is64Bit}[/]");
        AddRow("Rid", context.RuntimeIdentifier);
        AddRow("Vcpkg Triplet", context.Runtime.Triplet);
        AddRow("Cake Version", $"{context.Environment.Runtime.CakeVersion}");
        AddRow(".NET Version", $"{RuntimeInformation.FrameworkDescription}");
        AddRow("Working Dir", $"{context.Environment.WorkingDirectory.FullPath}");

        _console.Write(
            new Panel(grid)
                .Header("[yellow]Environment Details[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey)
                .Padding(1, 1)
        );
        _console.WriteLine();

        var sdkVersion = "[grey]Unknown[/]";
        await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("aqua"))
            .StartAsync("[aqua]Checking .NET SDK Version...[/]", _ =>
            {
                try
                {
                    var process = context.StartAndReturnProcess(
                        "dotnet",
                        new ProcessSettings { Arguments = "--version", RedirectStandardOutput = true, Silent = true }
                    );
                    process.WaitForExit();
                    var exitCode = process.GetExitCode();

                    if (exitCode == 0)
                    {
                        var output = process.GetStandardOutput()?.ToList();
                        if (output != null && output.Count != 0)
                        {
                            sdkVersion = $"[green]{Markup.Escape(string.Join(' ', output).Trim())}[/]";
                        }
                        else
                        {
                            sdkVersion = "[yellow]Obtained (No Output)[/]";
                        }
                    }
                    else
                    {
                        sdkVersion = $"[red]Failed (Exit Code: {exitCode})[/]";
                    }
                }
                catch (Win32Exception)
                {
                    sdkVersion = "[red]Not Found (Command failed)[/]";
                    context.Log.Error("dotnet --version command failed (Win32Exception). Is the .NET SDK in PATH?");
                }
                catch (Exception ex)
                {
                    sdkVersion = "[red]Error[/]";
                    context.Log.Verbose($"Checking dotnet --version failed: {ex.Message}");
                }

                return Task.FromResult(Task.CompletedTask);
            });

        _console.MarkupLine($"[bold aqua].NET SDK Version:[/] {sdkVersion}");
        _console.WriteLine();
    }
}
