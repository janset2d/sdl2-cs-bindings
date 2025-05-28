# pragma warning disable CA1031

using System.ComponentModel;
using System.Runtime.InteropServices;
using Build.Context;
using Build.Modules.Contracts;
using Cake.Common;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Spectre.Console;

namespace Build.Tasks.Common;

[TaskName("Info")]
public sealed class InfoTask : AsyncFrostingTask<BuildContext>
{
    private readonly IRuntimeProfile _runtimeProfile;

    public InfoTask(IRuntimeProfile runtimeProfile)
    {
        _runtimeProfile = runtimeProfile;
    }

    public override async Task RunAsync(BuildContext context)
    {
        AnsiConsole.Write(new FigletText("Build Info").Color(Color.CornflowerBlue));
        AnsiConsole.WriteLine();

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn();

        void AddRow(string key, string value) => grid.AddRow($"[bold aqua]{key}:[/]", value);

        AddRow("Operating System", $"{context.Environment.Platform.Family}");
        AddRow("OS Version", $"{Environment.OSVersion}");
        AddRow("OS Architecture", $"{RuntimeInformation.OSArchitecture}");
        AddRow("Is 64-bit OS", $"[{(context.Environment.Platform.Is64Bit ? "green" : "red")}]{context.Environment.Platform.Is64Bit}[/]");
        AddRow("Rid", _runtimeProfile.Rid);
        AddRow("Vcpkg Triplet", _runtimeProfile.Triplet);
        AddRow("Cake Version", $"{context.Environment.Runtime.CakeVersion}");
        AddRow(".NET Version", $"{RuntimeInformation.FrameworkDescription}");
        AddRow("Working Dir", $"{context.Environment.WorkingDirectory.FullPath}");

        AnsiConsole.Write(
            new Panel(grid)
                .Header("[yellow]Environment Details[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey)
                .Padding(1, 1)
        );
        AnsiConsole.WriteLine();

        var sdkVersion = "[grey]Unknown[/]";
        await AnsiConsole.Status()
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

        AnsiConsole.MarkupLine($"[bold aqua].NET SDK Version:[/] {sdkVersion}");
        AnsiConsole.WriteLine();
    }
}
