namespace Build.Modules.Contracts;

public interface IPackageConsumerSmokeRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
