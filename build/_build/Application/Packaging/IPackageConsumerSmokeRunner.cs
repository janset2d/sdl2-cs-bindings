namespace Build.Application.Packaging;

public interface IPackageConsumerSmokeRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
