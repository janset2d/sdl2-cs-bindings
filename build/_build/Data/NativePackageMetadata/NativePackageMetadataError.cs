using Build.Results;

namespace Build.Data.NativePackageMetadata;

public sealed class NativePackageMetadataError(string message, Exception? exception = null)
    : BuildError(message, exception);
