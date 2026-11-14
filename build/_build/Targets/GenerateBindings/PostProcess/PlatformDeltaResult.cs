using System.Runtime.InteropServices;

namespace Build.Targets.GenerateBindings.PostProcess;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct PlatformDeltaResult(
    int ProcessedFiles,
    int TransformedFiles,
    int RemovedDeclarations);
