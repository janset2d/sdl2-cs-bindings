using Build.Context;
using Build.Modules.DependencyAnalysis;
using Build.Tools.Dumpbin;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks.Dumpbin;

[TaskName("Dumpbin-Dependents")]
public class DependentsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.Information("Dumpbin dependents task started.");

        var dllsToProcess = new List<string>(context.DumpbinConfiguration.DllToDump);

        if (dllsToProcess.Count == 0)
        {
            context.Information("No specific DLLs provided via options. Scanning Vcpkg bin directory...");
            return;
        }

        context.Information($"Processing {dllsToProcess.Count} DLL(s)...");
        foreach (var dllPath in dllsToProcess)
        {
            var absoluteDllPath = context.MakeAbsolute(context.File(dllPath)).FullPath;
            context.Log.Information($"Processing: {absoluteDllPath}");

            try
            {
                var dumpbinSettings = new DumpbinDependentsSettings(absoluteDllPath)
                {
                    SetupProcessSettings = settings =>
                    {
                        settings.RedirectStandardOutput = true;
                        settings.RedirectStandardError = true;
                    },
                    PostAction = process =>
                    {
                        IEnumerable<string>? rawOutput = process.GetStandardOutput();
                        var dllFileName = context.File(absoluteDllPath).Path.GetFilename();
                        if (rawOutput != null)
                        {
                            context.Warning($"  No output received from dumpbin for {dllFileName}");
                        }

                        var parsedDlls = DumpbinParser.ExtractDependentDlls(rawOutput!);

                        if (parsedDlls.Count == 0)
                        {
                            context.Information($"  No dependents found for {dllFileName}");
                        }
                        else
                        {
                            context.Information($"  Dependents for {dllFileName}:");
                            foreach (var dependentDll in parsedDlls)
                            {
                                context.Information($"    - {dependentDll}");
                            }
                        }
                    },
                };

                context.DumpbinDependents(dumpbinSettings);
            }
            catch (CakeException ex)
            {
                context.Error($"  Failed to process {absoluteDllPath}: {ex.Message}");
            }
        }

        context.Information("Dumpbin dependents task finished.");
    }
}

