namespace Build.Targets.GenerateBindings.Parsing;

internal sealed record PlatformCatalog(IReadOnlyList<PlatformParseView> ParseViews)
{
    public static IReadOnlyList<string> AllPlatformMacros { get; } =
    [
        "_WIN32",
        "WIN32",
        "__WIN32__",
        "__WINDOWS__",
        "__WINRT__",
        "__GDK__",
        "__WINGDK__",
        "linux",
        "__linux",
        "__linux__",
        "__LINUX__",
        "__APPLE__",
        "__MACOSX__",
        "__IPHONEOS__",
        "__ANDROID__",
        "SDL_VIDEO_DRIVER_WINDOWS",
        "SDL_VIDEO_DRIVER_WINRT",
        "SDL_VIDEO_DRIVER_X11",
        "SDL_VIDEO_DRIVER_WAYLAND",
        "SDL_VIDEO_DRIVER_KMSDRM",
        "SDL_VIDEO_DRIVER_COCOA",
        "SDL_VIDEO_DRIVER_UIKIT",
        "SDL_VIDEO_DRIVER_ANDROID",
        "SDL_VIDEO_DRIVER_DIRECTFB",
        "SDL_VIDEO_DRIVER_VIVANTE",
        "SDL_VIDEO_DRIVER_MIR",
        "SDL_VIDEO_DRIVER_OS2",
    ];

    public static PlatformCatalog CreateSdl2Catalog()
    {
        return new PlatformCatalog(
        [
            View("Neutral", PlatformConditionKind.Neutral, null, []),
            View("WindowsDesktop", PlatformConditionKind.OperatingSystem, "windows", ["_WIN32=1", "WIN32=1", "__WIN32__=1", "__WINDOWS__=1", "SDL_VIDEO_DRIVER_WINDOWS=1"]),
            // WinRT/UWP first introduced as part of Windows 10 build 10240 (TH1, July 2015).
            // "windows10.0.10240.0" is the canonical [SupportedOSPlatform] string per CA1418 +
            // Microsoft.NET.SupportedPlatforms default list. A bare "windows" would tag
            // WinRT-only exports as available on every Windows release, which is incorrect.
            View("WinRT", PlatformConditionKind.OperatingSystem, "windows10.0.10240.0", ["_WIN32=1", "__WINRT__=1", "SDL_VIDEO_DRIVER_WINRT=1"]),
            View("GDK", PlatformConditionKind.OperatingSystem, "windows", ["_WIN32=1", "__GDK__=1", "__WINGDK__=1", "SDL_VIDEO_DRIVER_WINDOWS=1"]),
            View("Linux", PlatformConditionKind.OperatingSystem, "linux", ["linux=1", "__linux=1", "__linux__=1", "__LINUX__=1", "SDL_VIDEO_DRIVER_X11=1", "SDL_VIDEO_DRIVER_WAYLAND=1", "SDL_VIDEO_DRIVER_KMSDRM=1"]),
            // MAC_OS_X_VERSION_MIN_REQUIRED=1070 satisfies SDL_platform.h's "Mac OS X >= 10.7"
            // deployment-target #error check. SDL pulls this from <AvailabilityMacros.h> at
            // real-Apple-toolchain parse time; under our synthetic stub it must come from
            // catalog defines.
            //
            // SupportedOsPlatform uses "macos" (canonical per CA1418 + Microsoft.NET.SupportedPlatforms
            // default list, anchored to OperatingSystem.IsMacOS() guard). The legacy "osx" string
            // survives only on System.Runtime.InteropServices.OSPlatform.OSX for
            // RuntimeInformation.IsOSPlatform checks; platform-compat analyzers reject it.
            View("MacOS", PlatformConditionKind.OperatingSystem, "macos", ["__APPLE__=1", "__MACOSX__=1", "MAC_OS_X_VERSION_MIN_REQUIRED=1070", "SDL_VIDEO_DRIVER_COCOA=1"]),
            // TARGET_OS_IPHONE=1 takes SDL_platform.h's iOS branch, which self-defines
            // __IPHONEOS__ and skips the MAC_OS_X_VERSION_MIN_REQUIRED check entirely.
            View("IOS", PlatformConditionKind.OperatingSystem, "ios", ["__APPLE__=1", "__IPHONEOS__=1", "TARGET_OS_IPHONE=1", "SDL_VIDEO_DRIVER_UIKIT=1"]),
            View("Android", PlatformConditionKind.OperatingSystem, "android", ["__ANDROID__=1", "SDL_VIDEO_DRIVER_ANDROID=1"]),
        ]);
    }

    private static PlatformParseView View(
        string name,
        PlatformConditionKind kind,
        string? supportedOsPlatform,
        IReadOnlyList<string> defines)
    {
        var definedNames = defines
            .Select(value => value.Split('=', 2)[0])
            .ToHashSet(StringComparer.Ordinal);

        var undefines = AllPlatformMacros
            .Where(macro => !definedNames.Contains(macro))
            .ToArray();

        return new PlatformParseView(name, kind, supportedOsPlatform, defines, undefines);
    }
}
