namespace Build.Targets.GenerateBindings.PostProcess.Config;

internal sealed record PlatformView(string Name, string SupportedOs, IReadOnlyList<string> Defines);
