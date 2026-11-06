namespace Build.Host.Processes;

public interface IHostProcessRunner
{
    public Task<HostProcessResult> RunAsync(HostProcessRequest request, CancellationToken cancellationToken);
}
