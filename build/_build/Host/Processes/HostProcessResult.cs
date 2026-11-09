namespace Build.Host.Processes;

public sealed record HostProcessResult(int ExitCode, string StandardOutput, string StandardError);
