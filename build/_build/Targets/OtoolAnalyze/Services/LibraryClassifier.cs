namespace Build.Targets.OtoolAnalyze.Services;

public enum LibraryKind
{
    System,
    Framework,
    RPath,
    User,
}

public sealed record ClassifiedDependency(string Name, string Path, LibraryKind Kind, bool IsSystem);

public static class LibraryClassifier
{
    private static readonly string[] CommonSystemLibraries =
    [
        "libSystem.B.dylib",
        "libc++.1.dylib",
        "libobjc.A.dylib",
        "libz.1.dylib",
        "CoreFoundation.framework",
        "Foundation.framework",
        "AppKit.framework",
        "Cocoa.framework",
        "Carbon.framework",
        "IOKit.framework",
    ];

    public static ClassifiedDependency Classify(string libraryName, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(libraryName);
        ArgumentNullException.ThrowIfNull(fullPath);

        if (fullPath.StartsWith("/usr/lib/", StringComparison.Ordinal)
            || fullPath.StartsWith("/System/Library/", StringComparison.Ordinal))
        {
            var kind = libraryName.Contains(".framework", StringComparison.Ordinal) ? LibraryKind.Framework : LibraryKind.System;
            return new ClassifiedDependency(libraryName, fullPath, kind, IsSystem: true);
        }

        if (fullPath.StartsWith("@rpath/", StringComparison.Ordinal)
            || fullPath.StartsWith("@loader_path/", StringComparison.Ordinal)
            || fullPath.StartsWith("@executable_path/", StringComparison.Ordinal))
        {
            return new ClassifiedDependency(libraryName, fullPath, LibraryKind.RPath, IsSystem: false);
        }

        if (libraryName.Contains(".framework", StringComparison.Ordinal))
        {
            var isSystem = fullPath.Contains("/System/", StringComparison.Ordinal);
            return new ClassifiedDependency(libraryName, fullPath, LibraryKind.Framework, isSystem);
        }

        if (CommonSystemLibraries.Contains(libraryName, StringComparer.Ordinal))
        {
            return new ClassifiedDependency(libraryName, fullPath, LibraryKind.System, IsSystem: true);
        }

        return new ClassifiedDependency(libraryName, fullPath, LibraryKind.User, IsSystem: false);
    }
}
