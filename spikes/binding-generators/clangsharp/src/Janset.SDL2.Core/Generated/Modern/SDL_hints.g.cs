using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public enum SDL_HintPriority
    {
        SDL_HINT_DEFAULT,
        SDL_HINT_NORMAL,
        SDL_HINT_OVERRIDE,
    }

    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_SetHintWithPriority([NativeTypeName("const char *")] byte* name, [NativeTypeName("const char *")] byte* value, SDL_HintPriority priority);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_SetHint([NativeTypeName("const char *")] byte* name, [NativeTypeName("const char *")] byte* value);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_ResetHint([NativeTypeName("const char *")] byte* name);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_ResetHints();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GetHint([NativeTypeName("const char *")] byte* name);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GetHintBoolean([NativeTypeName("const char *")] byte* name, SDL_bool default_value);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_AddHintCallback([NativeTypeName("const char *")] byte* name, [NativeTypeName("SDL_HintCallback")] delegate* unmanaged[Cdecl]<nint, byte*, byte*, byte*, void> callback, [NativeTypeName("void*")] nint userdata);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_DelHintCallback([NativeTypeName("const char *")] byte* name, [NativeTypeName("SDL_HintCallback")] delegate* unmanaged[Cdecl]<nint, byte*, byte*, byte*, void> callback, [NativeTypeName("void*")] nint userdata);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_ClearHints();

        [NativeTypeName("#define SDL_HINT_ACCELEROMETER_AS_JOYSTICK \"SDL_ACCELEROMETER_AS_JOYSTICK\"")]
        public static ReadOnlySpan<byte> SDL_HINT_ACCELEROMETER_AS_JOYSTICK => "SDL_ACCELEROMETER_AS_JOYSTICK"u8;

        [NativeTypeName("#define SDL_HINT_ALLOW_ALT_TAB_WHILE_GRABBED \"SDL_ALLOW_ALT_TAB_WHILE_GRABBED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_ALLOW_ALT_TAB_WHILE_GRABBED => "SDL_ALLOW_ALT_TAB_WHILE_GRABBED"u8;

        [NativeTypeName("#define SDL_HINT_ALLOW_TOPMOST \"SDL_ALLOW_TOPMOST\"")]
        public static ReadOnlySpan<byte> SDL_HINT_ALLOW_TOPMOST => "SDL_ALLOW_TOPMOST"u8;

        [NativeTypeName("#define SDL_HINT_ANDROID_APK_EXPANSION_MAIN_FILE_VERSION \"SDL_ANDROID_APK_EXPANSION_MAIN_FILE_VERSION\"")]
        public static ReadOnlySpan<byte> SDL_HINT_ANDROID_APK_EXPANSION_MAIN_FILE_VERSION => "SDL_ANDROID_APK_EXPANSION_MAIN_FILE_VERSION"u8;

        [NativeTypeName("#define SDL_HINT_ANDROID_APK_EXPANSION_PATCH_FILE_VERSION \"SDL_ANDROID_APK_EXPANSION_PATCH_FILE_VERSION\"")]
        public static ReadOnlySpan<byte> SDL_HINT_ANDROID_APK_EXPANSION_PATCH_FILE_VERSION => "SDL_ANDROID_APK_EXPANSION_PATCH_FILE_VERSION"u8;

        [NativeTypeName("#define SDL_HINT_ANDROID_BLOCK_ON_PAUSE \"SDL_ANDROID_BLOCK_ON_PAUSE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_ANDROID_BLOCK_ON_PAUSE => "SDL_ANDROID_BLOCK_ON_PAUSE"u8;

        [NativeTypeName("#define SDL_HINT_ANDROID_BLOCK_ON_PAUSE_PAUSEAUDIO \"SDL_ANDROID_BLOCK_ON_PAUSE_PAUSEAUDIO\"")]
        public static ReadOnlySpan<byte> SDL_HINT_ANDROID_BLOCK_ON_PAUSE_PAUSEAUDIO => "SDL_ANDROID_BLOCK_ON_PAUSE_PAUSEAUDIO"u8;

        [NativeTypeName("#define SDL_HINT_ANDROID_TRAP_BACK_BUTTON \"SDL_ANDROID_TRAP_BACK_BUTTON\"")]
        public static ReadOnlySpan<byte> SDL_HINT_ANDROID_TRAP_BACK_BUTTON => "SDL_ANDROID_TRAP_BACK_BUTTON"u8;

        [NativeTypeName("#define SDL_HINT_APP_NAME \"SDL_APP_NAME\"")]
        public static ReadOnlySpan<byte> SDL_HINT_APP_NAME => "SDL_APP_NAME"u8;

        [NativeTypeName("#define SDL_HINT_APPLE_TV_CONTROLLER_UI_EVENTS \"SDL_APPLE_TV_CONTROLLER_UI_EVENTS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_APPLE_TV_CONTROLLER_UI_EVENTS => "SDL_APPLE_TV_CONTROLLER_UI_EVENTS"u8;

        [NativeTypeName("#define SDL_HINT_APPLE_TV_REMOTE_ALLOW_ROTATION \"SDL_APPLE_TV_REMOTE_ALLOW_ROTATION\"")]
        public static ReadOnlySpan<byte> SDL_HINT_APPLE_TV_REMOTE_ALLOW_ROTATION => "SDL_APPLE_TV_REMOTE_ALLOW_ROTATION"u8;

        [NativeTypeName("#define SDL_HINT_AUDIO_CATEGORY \"SDL_AUDIO_CATEGORY\"")]
        public static ReadOnlySpan<byte> SDL_HINT_AUDIO_CATEGORY => "SDL_AUDIO_CATEGORY"u8;

        [NativeTypeName("#define SDL_HINT_AUDIO_DEVICE_APP_NAME \"SDL_AUDIO_DEVICE_APP_NAME\"")]
        public static ReadOnlySpan<byte> SDL_HINT_AUDIO_DEVICE_APP_NAME => "SDL_AUDIO_DEVICE_APP_NAME"u8;

        [NativeTypeName("#define SDL_HINT_AUDIO_DEVICE_STREAM_NAME \"SDL_AUDIO_DEVICE_STREAM_NAME\"")]
        public static ReadOnlySpan<byte> SDL_HINT_AUDIO_DEVICE_STREAM_NAME => "SDL_AUDIO_DEVICE_STREAM_NAME"u8;

        [NativeTypeName("#define SDL_HINT_AUDIO_DEVICE_STREAM_ROLE \"SDL_AUDIO_DEVICE_STREAM_ROLE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_AUDIO_DEVICE_STREAM_ROLE => "SDL_AUDIO_DEVICE_STREAM_ROLE"u8;

        [NativeTypeName("#define SDL_HINT_AUDIO_RESAMPLING_MODE \"SDL_AUDIO_RESAMPLING_MODE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_AUDIO_RESAMPLING_MODE => "SDL_AUDIO_RESAMPLING_MODE"u8;

        [NativeTypeName("#define SDL_HINT_AUTO_UPDATE_JOYSTICKS \"SDL_AUTO_UPDATE_JOYSTICKS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_AUTO_UPDATE_JOYSTICKS => "SDL_AUTO_UPDATE_JOYSTICKS"u8;

        [NativeTypeName("#define SDL_HINT_AUTO_UPDATE_SENSORS \"SDL_AUTO_UPDATE_SENSORS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_AUTO_UPDATE_SENSORS => "SDL_AUTO_UPDATE_SENSORS"u8;

        [NativeTypeName("#define SDL_HINT_BMP_SAVE_LEGACY_FORMAT \"SDL_BMP_SAVE_LEGACY_FORMAT\"")]
        public static ReadOnlySpan<byte> SDL_HINT_BMP_SAVE_LEGACY_FORMAT => "SDL_BMP_SAVE_LEGACY_FORMAT"u8;

        [NativeTypeName("#define SDL_HINT_DISPLAY_USABLE_BOUNDS \"SDL_DISPLAY_USABLE_BOUNDS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_DISPLAY_USABLE_BOUNDS => "SDL_DISPLAY_USABLE_BOUNDS"u8;

        [NativeTypeName("#define SDL_HINT_EMSCRIPTEN_ASYNCIFY \"SDL_EMSCRIPTEN_ASYNCIFY\"")]
        public static ReadOnlySpan<byte> SDL_HINT_EMSCRIPTEN_ASYNCIFY => "SDL_EMSCRIPTEN_ASYNCIFY"u8;

        [NativeTypeName("#define SDL_HINT_EMSCRIPTEN_KEYBOARD_ELEMENT \"SDL_EMSCRIPTEN_KEYBOARD_ELEMENT\"")]
        public static ReadOnlySpan<byte> SDL_HINT_EMSCRIPTEN_KEYBOARD_ELEMENT => "SDL_EMSCRIPTEN_KEYBOARD_ELEMENT"u8;

        [NativeTypeName("#define SDL_HINT_ENABLE_SCREEN_KEYBOARD \"SDL_ENABLE_SCREEN_KEYBOARD\"")]
        public static ReadOnlySpan<byte> SDL_HINT_ENABLE_SCREEN_KEYBOARD => "SDL_ENABLE_SCREEN_KEYBOARD"u8;

        [NativeTypeName("#define SDL_HINT_EVENT_LOGGING \"SDL_EVENT_LOGGING\"")]
        public static ReadOnlySpan<byte> SDL_HINT_EVENT_LOGGING => "SDL_EVENT_LOGGING"u8;

        [NativeTypeName("#define SDL_HINT_FORCE_RAISEWINDOW \"SDL_HINT_FORCE_RAISEWINDOW\"")]
        public static ReadOnlySpan<byte> SDL_HINT_FORCE_RAISEWINDOW => "SDL_HINT_FORCE_RAISEWINDOW"u8;

        [NativeTypeName("#define SDL_HINT_FRAMEBUFFER_ACCELERATION \"SDL_FRAMEBUFFER_ACCELERATION\"")]
        public static ReadOnlySpan<byte> SDL_HINT_FRAMEBUFFER_ACCELERATION => "SDL_FRAMEBUFFER_ACCELERATION"u8;

        [NativeTypeName("#define SDL_HINT_GAMECONTROLLERCONFIG \"SDL_GAMECONTROLLERCONFIG\"")]
        public static ReadOnlySpan<byte> SDL_HINT_GAMECONTROLLERCONFIG => "SDL_GAMECONTROLLERCONFIG"u8;

        [NativeTypeName("#define SDL_HINT_GAMECONTROLLERCONFIG_FILE \"SDL_GAMECONTROLLERCONFIG_FILE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_GAMECONTROLLERCONFIG_FILE => "SDL_GAMECONTROLLERCONFIG_FILE"u8;

        [NativeTypeName("#define SDL_HINT_GAMECONTROLLERTYPE \"SDL_GAMECONTROLLERTYPE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_GAMECONTROLLERTYPE => "SDL_GAMECONTROLLERTYPE"u8;

        [NativeTypeName("#define SDL_HINT_GAMECONTROLLER_IGNORE_DEVICES \"SDL_GAMECONTROLLER_IGNORE_DEVICES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_GAMECONTROLLER_IGNORE_DEVICES => "SDL_GAMECONTROLLER_IGNORE_DEVICES"u8;

        [NativeTypeName("#define SDL_HINT_GAMECONTROLLER_IGNORE_DEVICES_EXCEPT \"SDL_GAMECONTROLLER_IGNORE_DEVICES_EXCEPT\"")]
        public static ReadOnlySpan<byte> SDL_HINT_GAMECONTROLLER_IGNORE_DEVICES_EXCEPT => "SDL_GAMECONTROLLER_IGNORE_DEVICES_EXCEPT"u8;

        [NativeTypeName("#define SDL_HINT_GAMECONTROLLER_USE_BUTTON_LABELS \"SDL_GAMECONTROLLER_USE_BUTTON_LABELS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_GAMECONTROLLER_USE_BUTTON_LABELS => "SDL_GAMECONTROLLER_USE_BUTTON_LABELS"u8;

        [NativeTypeName("#define SDL_HINT_GRAB_KEYBOARD \"SDL_GRAB_KEYBOARD\"")]
        public static ReadOnlySpan<byte> SDL_HINT_GRAB_KEYBOARD => "SDL_GRAB_KEYBOARD"u8;

        [NativeTypeName("#define SDL_HINT_HIDAPI_IGNORE_DEVICES \"SDL_HIDAPI_IGNORE_DEVICES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_HIDAPI_IGNORE_DEVICES => "SDL_HIDAPI_IGNORE_DEVICES"u8;

        [NativeTypeName("#define SDL_HINT_IDLE_TIMER_DISABLED \"SDL_IOS_IDLE_TIMER_DISABLED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_IDLE_TIMER_DISABLED => "SDL_IOS_IDLE_TIMER_DISABLED"u8;

        [NativeTypeName("#define SDL_HINT_IME_INTERNAL_EDITING \"SDL_IME_INTERNAL_EDITING\"")]
        public static ReadOnlySpan<byte> SDL_HINT_IME_INTERNAL_EDITING => "SDL_IME_INTERNAL_EDITING"u8;

        [NativeTypeName("#define SDL_HINT_IME_SHOW_UI \"SDL_IME_SHOW_UI\"")]
        public static ReadOnlySpan<byte> SDL_HINT_IME_SHOW_UI => "SDL_IME_SHOW_UI"u8;

        [NativeTypeName("#define SDL_HINT_IME_SUPPORT_EXTENDED_TEXT \"SDL_IME_SUPPORT_EXTENDED_TEXT\"")]
        public static ReadOnlySpan<byte> SDL_HINT_IME_SUPPORT_EXTENDED_TEXT => "SDL_IME_SUPPORT_EXTENDED_TEXT"u8;

        [NativeTypeName("#define SDL_HINT_IOS_HIDE_HOME_INDICATOR \"SDL_IOS_HIDE_HOME_INDICATOR\"")]
        public static ReadOnlySpan<byte> SDL_HINT_IOS_HIDE_HOME_INDICATOR => "SDL_IOS_HIDE_HOME_INDICATOR"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS \"SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS => "SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_ARCADESTICK_DEVICES \"SDL_JOYSTICK_ARCADESTICK_DEVICES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_ARCADESTICK_DEVICES => "SDL_JOYSTICK_ARCADESTICK_DEVICES"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_ARCADESTICK_DEVICES_EXCLUDED \"SDL_JOYSTICK_ARCADESTICK_DEVICES_EXCLUDED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_ARCADESTICK_DEVICES_EXCLUDED => "SDL_JOYSTICK_ARCADESTICK_DEVICES_EXCLUDED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_BLACKLIST_DEVICES \"SDL_JOYSTICK_BLACKLIST_DEVICES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_BLACKLIST_DEVICES => "SDL_JOYSTICK_BLACKLIST_DEVICES"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_BLACKLIST_DEVICES_EXCLUDED \"SDL_JOYSTICK_BLACKLIST_DEVICES_EXCLUDED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_BLACKLIST_DEVICES_EXCLUDED => "SDL_JOYSTICK_BLACKLIST_DEVICES_EXCLUDED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_FLIGHTSTICK_DEVICES \"SDL_JOYSTICK_FLIGHTSTICK_DEVICES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_FLIGHTSTICK_DEVICES => "SDL_JOYSTICK_FLIGHTSTICK_DEVICES"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_FLIGHTSTICK_DEVICES_EXCLUDED \"SDL_JOYSTICK_FLIGHTSTICK_DEVICES_EXCLUDED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_FLIGHTSTICK_DEVICES_EXCLUDED => "SDL_JOYSTICK_FLIGHTSTICK_DEVICES_EXCLUDED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_GAMECUBE_DEVICES \"SDL_JOYSTICK_GAMECUBE_DEVICES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_GAMECUBE_DEVICES => "SDL_JOYSTICK_GAMECUBE_DEVICES"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_GAMECUBE_DEVICES_EXCLUDED \"SDL_JOYSTICK_GAMECUBE_DEVICES_EXCLUDED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_GAMECUBE_DEVICES_EXCLUDED => "SDL_JOYSTICK_GAMECUBE_DEVICES_EXCLUDED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI \"SDL_JOYSTICK_HIDAPI\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI => "SDL_JOYSTICK_HIDAPI"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_GAMECUBE \"SDL_JOYSTICK_HIDAPI_GAMECUBE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_GAMECUBE => "SDL_JOYSTICK_HIDAPI_GAMECUBE"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_GAMECUBE_RUMBLE_BRAKE \"SDL_JOYSTICK_GAMECUBE_RUMBLE_BRAKE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_GAMECUBE_RUMBLE_BRAKE => "SDL_JOYSTICK_GAMECUBE_RUMBLE_BRAKE"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_JOY_CONS \"SDL_JOYSTICK_HIDAPI_JOY_CONS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_JOY_CONS => "SDL_JOYSTICK_HIDAPI_JOY_CONS"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_COMBINE_JOY_CONS \"SDL_JOYSTICK_HIDAPI_COMBINE_JOY_CONS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_COMBINE_JOY_CONS => "SDL_JOYSTICK_HIDAPI_COMBINE_JOY_CONS"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_VERTICAL_JOY_CONS \"SDL_JOYSTICK_HIDAPI_VERTICAL_JOY_CONS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_VERTICAL_JOY_CONS => "SDL_JOYSTICK_HIDAPI_VERTICAL_JOY_CONS"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_LUNA \"SDL_JOYSTICK_HIDAPI_LUNA\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_LUNA => "SDL_JOYSTICK_HIDAPI_LUNA"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_NINTENDO_CLASSIC \"SDL_JOYSTICK_HIDAPI_NINTENDO_CLASSIC\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_NINTENDO_CLASSIC => "SDL_JOYSTICK_HIDAPI_NINTENDO_CLASSIC"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_SHIELD \"SDL_JOYSTICK_HIDAPI_SHIELD\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_SHIELD => "SDL_JOYSTICK_HIDAPI_SHIELD"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_PS3 \"SDL_JOYSTICK_HIDAPI_PS3\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_PS3 => "SDL_JOYSTICK_HIDAPI_PS3"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_PS4 \"SDL_JOYSTICK_HIDAPI_PS4\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_PS4 => "SDL_JOYSTICK_HIDAPI_PS4"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_PS4_RUMBLE \"SDL_JOYSTICK_HIDAPI_PS4_RUMBLE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_PS4_RUMBLE => "SDL_JOYSTICK_HIDAPI_PS4_RUMBLE"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_PS5 \"SDL_JOYSTICK_HIDAPI_PS5\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_PS5 => "SDL_JOYSTICK_HIDAPI_PS5"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_PS5_PLAYER_LED \"SDL_JOYSTICK_HIDAPI_PS5_PLAYER_LED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_PS5_PLAYER_LED => "SDL_JOYSTICK_HIDAPI_PS5_PLAYER_LED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_PS5_RUMBLE \"SDL_JOYSTICK_HIDAPI_PS5_RUMBLE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_PS5_RUMBLE => "SDL_JOYSTICK_HIDAPI_PS5_RUMBLE"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_STADIA \"SDL_JOYSTICK_HIDAPI_STADIA\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_STADIA => "SDL_JOYSTICK_HIDAPI_STADIA"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_STEAM \"SDL_JOYSTICK_HIDAPI_STEAM\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_STEAM => "SDL_JOYSTICK_HIDAPI_STEAM"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_STEAMDECK \"SDL_JOYSTICK_HIDAPI_STEAMDECK\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_STEAMDECK => "SDL_JOYSTICK_HIDAPI_STEAMDECK"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_SWITCH \"SDL_JOYSTICK_HIDAPI_SWITCH\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_SWITCH => "SDL_JOYSTICK_HIDAPI_SWITCH"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_SWITCH_HOME_LED \"SDL_JOYSTICK_HIDAPI_SWITCH_HOME_LED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_SWITCH_HOME_LED => "SDL_JOYSTICK_HIDAPI_SWITCH_HOME_LED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_JOYCON_HOME_LED \"SDL_JOYSTICK_HIDAPI_JOYCON_HOME_LED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_JOYCON_HOME_LED => "SDL_JOYSTICK_HIDAPI_JOYCON_HOME_LED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_SWITCH_PLAYER_LED \"SDL_JOYSTICK_HIDAPI_SWITCH_PLAYER_LED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_SWITCH_PLAYER_LED => "SDL_JOYSTICK_HIDAPI_SWITCH_PLAYER_LED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_WII \"SDL_JOYSTICK_HIDAPI_WII\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_WII => "SDL_JOYSTICK_HIDAPI_WII"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_WII_PLAYER_LED \"SDL_JOYSTICK_HIDAPI_WII_PLAYER_LED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_WII_PLAYER_LED => "SDL_JOYSTICK_HIDAPI_WII_PLAYER_LED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_XBOX \"SDL_JOYSTICK_HIDAPI_XBOX\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_XBOX => "SDL_JOYSTICK_HIDAPI_XBOX"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_XBOX_360 \"SDL_JOYSTICK_HIDAPI_XBOX_360\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_XBOX_360 => "SDL_JOYSTICK_HIDAPI_XBOX_360"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_XBOX_360_PLAYER_LED \"SDL_JOYSTICK_HIDAPI_XBOX_360_PLAYER_LED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_XBOX_360_PLAYER_LED => "SDL_JOYSTICK_HIDAPI_XBOX_360_PLAYER_LED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_XBOX_360_WIRELESS \"SDL_JOYSTICK_HIDAPI_XBOX_360_WIRELESS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_XBOX_360_WIRELESS => "SDL_JOYSTICK_HIDAPI_XBOX_360_WIRELESS"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_XBOX_ONE \"SDL_JOYSTICK_HIDAPI_XBOX_ONE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_XBOX_ONE => "SDL_JOYSTICK_HIDAPI_XBOX_ONE"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HIDAPI_XBOX_ONE_HOME_LED \"SDL_JOYSTICK_HIDAPI_XBOX_ONE_HOME_LED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HIDAPI_XBOX_ONE_HOME_LED => "SDL_JOYSTICK_HIDAPI_XBOX_ONE_HOME_LED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_IOKIT \"SDL_JOYSTICK_IOKIT\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_IOKIT => "SDL_JOYSTICK_IOKIT"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_MFI \"SDL_JOYSTICK_MFI\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_MFI => "SDL_JOYSTICK_MFI"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_RAWINPUT \"SDL_JOYSTICK_RAWINPUT\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_RAWINPUT => "SDL_JOYSTICK_RAWINPUT"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_RAWINPUT_CORRELATE_XINPUT \"SDL_JOYSTICK_RAWINPUT_CORRELATE_XINPUT\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_RAWINPUT_CORRELATE_XINPUT => "SDL_JOYSTICK_RAWINPUT_CORRELATE_XINPUT"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_ROG_CHAKRAM \"SDL_JOYSTICK_ROG_CHAKRAM\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_ROG_CHAKRAM => "SDL_JOYSTICK_ROG_CHAKRAM"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_THREAD \"SDL_JOYSTICK_THREAD\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_THREAD => "SDL_JOYSTICK_THREAD"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_THROTTLE_DEVICES \"SDL_JOYSTICK_THROTTLE_DEVICES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_THROTTLE_DEVICES => "SDL_JOYSTICK_THROTTLE_DEVICES"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_THROTTLE_DEVICES_EXCLUDED \"SDL_JOYSTICK_THROTTLE_DEVICES_EXCLUDED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_THROTTLE_DEVICES_EXCLUDED => "SDL_JOYSTICK_THROTTLE_DEVICES_EXCLUDED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_WGI \"SDL_JOYSTICK_WGI\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_WGI => "SDL_JOYSTICK_WGI"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_WHEEL_DEVICES \"SDL_JOYSTICK_WHEEL_DEVICES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_WHEEL_DEVICES => "SDL_JOYSTICK_WHEEL_DEVICES"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_WHEEL_DEVICES_EXCLUDED \"SDL_JOYSTICK_WHEEL_DEVICES_EXCLUDED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_WHEEL_DEVICES_EXCLUDED => "SDL_JOYSTICK_WHEEL_DEVICES_EXCLUDED"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_ZERO_CENTERED_DEVICES \"SDL_JOYSTICK_ZERO_CENTERED_DEVICES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_ZERO_CENTERED_DEVICES => "SDL_JOYSTICK_ZERO_CENTERED_DEVICES"u8;

        [NativeTypeName("#define SDL_HINT_KMSDRM_REQUIRE_DRM_MASTER \"SDL_KMSDRM_REQUIRE_DRM_MASTER\"")]
        public static ReadOnlySpan<byte> SDL_HINT_KMSDRM_REQUIRE_DRM_MASTER => "SDL_KMSDRM_REQUIRE_DRM_MASTER"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_DEVICE \"SDL_JOYSTICK_DEVICE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_DEVICE => "SDL_JOYSTICK_DEVICE"u8;

        [NativeTypeName("#define SDL_HINT_JOYSTICK_HAPTIC_AXES \"SDL_JOYSTICK_HAPTIC_AXES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_JOYSTICK_HAPTIC_AXES => "SDL_JOYSTICK_HAPTIC_AXES"u8;

        [NativeTypeName("#define SDL_HINT_LINUX_DIGITAL_HATS \"SDL_LINUX_DIGITAL_HATS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_LINUX_DIGITAL_HATS => "SDL_LINUX_DIGITAL_HATS"u8;

        [NativeTypeName("#define SDL_HINT_LINUX_HAT_DEADZONES \"SDL_LINUX_HAT_DEADZONES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_LINUX_HAT_DEADZONES => "SDL_LINUX_HAT_DEADZONES"u8;

        [NativeTypeName("#define SDL_HINT_LINUX_JOYSTICK_CLASSIC \"SDL_LINUX_JOYSTICK_CLASSIC\"")]
        public static ReadOnlySpan<byte> SDL_HINT_LINUX_JOYSTICK_CLASSIC => "SDL_LINUX_JOYSTICK_CLASSIC"u8;

        [NativeTypeName("#define SDL_HINT_LINUX_JOYSTICK_DEADZONES \"SDL_LINUX_JOYSTICK_DEADZONES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_LINUX_JOYSTICK_DEADZONES => "SDL_LINUX_JOYSTICK_DEADZONES"u8;

        [NativeTypeName("#define SDL_HINT_LOGGING \"SDL_LOGGING\"")]
        public static ReadOnlySpan<byte> SDL_HINT_LOGGING => "SDL_LOGGING"u8;

        [NativeTypeName("#define SDL_HINT_MAC_BACKGROUND_APP \"SDL_MAC_BACKGROUND_APP\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MAC_BACKGROUND_APP => "SDL_MAC_BACKGROUND_APP"u8;

        [NativeTypeName("#define SDL_HINT_MAC_CTRL_CLICK_EMULATE_RIGHT_CLICK \"SDL_MAC_CTRL_CLICK_EMULATE_RIGHT_CLICK\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MAC_CTRL_CLICK_EMULATE_RIGHT_CLICK => "SDL_MAC_CTRL_CLICK_EMULATE_RIGHT_CLICK"u8;

        [NativeTypeName("#define SDL_HINT_MAC_OPENGL_ASYNC_DISPATCH \"SDL_MAC_OPENGL_ASYNC_DISPATCH\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MAC_OPENGL_ASYNC_DISPATCH => "SDL_MAC_OPENGL_ASYNC_DISPATCH"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_DOUBLE_CLICK_RADIUS \"SDL_MOUSE_DOUBLE_CLICK_RADIUS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_DOUBLE_CLICK_RADIUS => "SDL_MOUSE_DOUBLE_CLICK_RADIUS"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_DOUBLE_CLICK_TIME \"SDL_MOUSE_DOUBLE_CLICK_TIME\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_DOUBLE_CLICK_TIME => "SDL_MOUSE_DOUBLE_CLICK_TIME"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH \"SDL_MOUSE_FOCUS_CLICKTHROUGH\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH => "SDL_MOUSE_FOCUS_CLICKTHROUGH"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_NORMAL_SPEED_SCALE \"SDL_MOUSE_NORMAL_SPEED_SCALE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_NORMAL_SPEED_SCALE => "SDL_MOUSE_NORMAL_SPEED_SCALE"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_RELATIVE_MODE_CENTER \"SDL_MOUSE_RELATIVE_MODE_CENTER\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_RELATIVE_MODE_CENTER => "SDL_MOUSE_RELATIVE_MODE_CENTER"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_RELATIVE_MODE_WARP \"SDL_MOUSE_RELATIVE_MODE_WARP\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_RELATIVE_MODE_WARP => "SDL_MOUSE_RELATIVE_MODE_WARP"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_RELATIVE_SCALING \"SDL_MOUSE_RELATIVE_SCALING\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_RELATIVE_SCALING => "SDL_MOUSE_RELATIVE_SCALING"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_RELATIVE_SPEED_SCALE \"SDL_MOUSE_RELATIVE_SPEED_SCALE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_RELATIVE_SPEED_SCALE => "SDL_MOUSE_RELATIVE_SPEED_SCALE"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_RELATIVE_SYSTEM_SCALE \"SDL_MOUSE_RELATIVE_SYSTEM_SCALE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_RELATIVE_SYSTEM_SCALE => "SDL_MOUSE_RELATIVE_SYSTEM_SCALE"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_RELATIVE_WARP_MOTION \"SDL_MOUSE_RELATIVE_WARP_MOTION\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_RELATIVE_WARP_MOTION => "SDL_MOUSE_RELATIVE_WARP_MOTION"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_RELATIVE_CURSOR_VISIBLE \"SDL_MOUSE_RELATIVE_CURSOR_VISIBLE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_RELATIVE_CURSOR_VISIBLE => "SDL_MOUSE_RELATIVE_CURSOR_VISIBLE"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_TOUCH_EVENTS \"SDL_MOUSE_TOUCH_EVENTS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_TOUCH_EVENTS => "SDL_MOUSE_TOUCH_EVENTS"u8;

        [NativeTypeName("#define SDL_HINT_MOUSE_AUTO_CAPTURE \"SDL_MOUSE_AUTO_CAPTURE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_MOUSE_AUTO_CAPTURE => "SDL_MOUSE_AUTO_CAPTURE"u8;

        [NativeTypeName("#define SDL_HINT_NO_SIGNAL_HANDLERS \"SDL_NO_SIGNAL_HANDLERS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_NO_SIGNAL_HANDLERS => "SDL_NO_SIGNAL_HANDLERS"u8;

        [NativeTypeName("#define SDL_HINT_OPENGL_ES_DRIVER \"SDL_OPENGL_ES_DRIVER\"")]
        public static ReadOnlySpan<byte> SDL_HINT_OPENGL_ES_DRIVER => "SDL_OPENGL_ES_DRIVER"u8;

        [NativeTypeName("#define SDL_HINT_ORIENTATIONS \"SDL_IOS_ORIENTATIONS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_ORIENTATIONS => "SDL_IOS_ORIENTATIONS"u8;

        [NativeTypeName("#define SDL_HINT_POLL_SENTINEL \"SDL_POLL_SENTINEL\"")]
        public static ReadOnlySpan<byte> SDL_HINT_POLL_SENTINEL => "SDL_POLL_SENTINEL"u8;

        [NativeTypeName("#define SDL_HINT_PREFERRED_LOCALES \"SDL_PREFERRED_LOCALES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_PREFERRED_LOCALES => "SDL_PREFERRED_LOCALES"u8;

        [NativeTypeName("#define SDL_HINT_QTWAYLAND_CONTENT_ORIENTATION \"SDL_QTWAYLAND_CONTENT_ORIENTATION\"")]
        public static ReadOnlySpan<byte> SDL_HINT_QTWAYLAND_CONTENT_ORIENTATION => "SDL_QTWAYLAND_CONTENT_ORIENTATION"u8;

        [NativeTypeName("#define SDL_HINT_QTWAYLAND_WINDOW_FLAGS \"SDL_QTWAYLAND_WINDOW_FLAGS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_QTWAYLAND_WINDOW_FLAGS => "SDL_QTWAYLAND_WINDOW_FLAGS"u8;

        [NativeTypeName("#define SDL_HINT_RENDER_BATCHING \"SDL_RENDER_BATCHING\"")]
        public static ReadOnlySpan<byte> SDL_HINT_RENDER_BATCHING => "SDL_RENDER_BATCHING"u8;

        [NativeTypeName("#define SDL_HINT_RENDER_LINE_METHOD \"SDL_RENDER_LINE_METHOD\"")]
        public static ReadOnlySpan<byte> SDL_HINT_RENDER_LINE_METHOD => "SDL_RENDER_LINE_METHOD"u8;

        [NativeTypeName("#define SDL_HINT_RENDER_DIRECT3D11_DEBUG \"SDL_RENDER_DIRECT3D11_DEBUG\"")]
        public static ReadOnlySpan<byte> SDL_HINT_RENDER_DIRECT3D11_DEBUG => "SDL_RENDER_DIRECT3D11_DEBUG"u8;

        [NativeTypeName("#define SDL_HINT_RENDER_DIRECT3D_THREADSAFE \"SDL_RENDER_DIRECT3D_THREADSAFE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_RENDER_DIRECT3D_THREADSAFE => "SDL_RENDER_DIRECT3D_THREADSAFE"u8;

        [NativeTypeName("#define SDL_HINT_RENDER_DRIVER \"SDL_RENDER_DRIVER\"")]
        public static ReadOnlySpan<byte> SDL_HINT_RENDER_DRIVER => "SDL_RENDER_DRIVER"u8;

        [NativeTypeName("#define SDL_HINT_RENDER_LOGICAL_SIZE_MODE \"SDL_RENDER_LOGICAL_SIZE_MODE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_RENDER_LOGICAL_SIZE_MODE => "SDL_RENDER_LOGICAL_SIZE_MODE"u8;

        [NativeTypeName("#define SDL_HINT_RENDER_OPENGL_SHADERS \"SDL_RENDER_OPENGL_SHADERS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_RENDER_OPENGL_SHADERS => "SDL_RENDER_OPENGL_SHADERS"u8;

        [NativeTypeName("#define SDL_HINT_RENDER_SCALE_QUALITY \"SDL_RENDER_SCALE_QUALITY\"")]
        public static ReadOnlySpan<byte> SDL_HINT_RENDER_SCALE_QUALITY => "SDL_RENDER_SCALE_QUALITY"u8;

        [NativeTypeName("#define SDL_HINT_RENDER_VSYNC \"SDL_RENDER_VSYNC\"")]
        public static ReadOnlySpan<byte> SDL_HINT_RENDER_VSYNC => "SDL_RENDER_VSYNC"u8;

        [NativeTypeName("#define SDL_HINT_RENDER_METAL_PREFER_LOW_POWER_DEVICE \"SDL_RENDER_METAL_PREFER_LOW_POWER_DEVICE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_RENDER_METAL_PREFER_LOW_POWER_DEVICE => "SDL_RENDER_METAL_PREFER_LOW_POWER_DEVICE"u8;

        [NativeTypeName("#define SDL_HINT_ROG_GAMEPAD_MICE \"SDL_ROG_GAMEPAD_MICE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_ROG_GAMEPAD_MICE => "SDL_ROG_GAMEPAD_MICE"u8;

        [NativeTypeName("#define SDL_HINT_ROG_GAMEPAD_MICE_EXCLUDED \"SDL_ROG_GAMEPAD_MICE_EXCLUDED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_ROG_GAMEPAD_MICE_EXCLUDED => "SDL_ROG_GAMEPAD_MICE_EXCLUDED"u8;

        [NativeTypeName("#define SDL_HINT_PS2_DYNAMIC_VSYNC \"SDL_PS2_DYNAMIC_VSYNC\"")]
        public static ReadOnlySpan<byte> SDL_HINT_PS2_DYNAMIC_VSYNC => "SDL_PS2_DYNAMIC_VSYNC"u8;

        [NativeTypeName("#define SDL_HINT_RETURN_KEY_HIDES_IME \"SDL_RETURN_KEY_HIDES_IME\"")]
        public static ReadOnlySpan<byte> SDL_HINT_RETURN_KEY_HIDES_IME => "SDL_RETURN_KEY_HIDES_IME"u8;

        [NativeTypeName("#define SDL_HINT_RPI_VIDEO_LAYER \"SDL_RPI_VIDEO_LAYER\"")]
        public static ReadOnlySpan<byte> SDL_HINT_RPI_VIDEO_LAYER => "SDL_RPI_VIDEO_LAYER"u8;

        [NativeTypeName("#define SDL_HINT_SCREENSAVER_INHIBIT_ACTIVITY_NAME \"SDL_SCREENSAVER_INHIBIT_ACTIVITY_NAME\"")]
        public static ReadOnlySpan<byte> SDL_HINT_SCREENSAVER_INHIBIT_ACTIVITY_NAME => "SDL_SCREENSAVER_INHIBIT_ACTIVITY_NAME"u8;

        [NativeTypeName("#define SDL_HINT_THREAD_FORCE_REALTIME_TIME_CRITICAL \"SDL_THREAD_FORCE_REALTIME_TIME_CRITICAL\"")]
        public static ReadOnlySpan<byte> SDL_HINT_THREAD_FORCE_REALTIME_TIME_CRITICAL => "SDL_THREAD_FORCE_REALTIME_TIME_CRITICAL"u8;

        [NativeTypeName("#define SDL_HINT_THREAD_PRIORITY_POLICY \"SDL_THREAD_PRIORITY_POLICY\"")]
        public static ReadOnlySpan<byte> SDL_HINT_THREAD_PRIORITY_POLICY => "SDL_THREAD_PRIORITY_POLICY"u8;

        [NativeTypeName("#define SDL_HINT_THREAD_STACK_SIZE \"SDL_THREAD_STACK_SIZE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_THREAD_STACK_SIZE => "SDL_THREAD_STACK_SIZE"u8;

        [NativeTypeName("#define SDL_HINT_TIMER_RESOLUTION \"SDL_TIMER_RESOLUTION\"")]
        public static ReadOnlySpan<byte> SDL_HINT_TIMER_RESOLUTION => "SDL_TIMER_RESOLUTION"u8;

        [NativeTypeName("#define SDL_HINT_TOUCH_MOUSE_EVENTS \"SDL_TOUCH_MOUSE_EVENTS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_TOUCH_MOUSE_EVENTS => "SDL_TOUCH_MOUSE_EVENTS"u8;

        [NativeTypeName("#define SDL_HINT_VITA_TOUCH_MOUSE_DEVICE \"SDL_HINT_VITA_TOUCH_MOUSE_DEVICE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VITA_TOUCH_MOUSE_DEVICE => "SDL_HINT_VITA_TOUCH_MOUSE_DEVICE"u8;

        [NativeTypeName("#define SDL_HINT_TV_REMOTE_AS_JOYSTICK \"SDL_TV_REMOTE_AS_JOYSTICK\"")]
        public static ReadOnlySpan<byte> SDL_HINT_TV_REMOTE_AS_JOYSTICK => "SDL_TV_REMOTE_AS_JOYSTICK"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_ALLOW_SCREENSAVER \"SDL_VIDEO_ALLOW_SCREENSAVER\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_ALLOW_SCREENSAVER => "SDL_VIDEO_ALLOW_SCREENSAVER"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_DOUBLE_BUFFER \"SDL_VIDEO_DOUBLE_BUFFER\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_DOUBLE_BUFFER => "SDL_VIDEO_DOUBLE_BUFFER"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_EGL_ALLOW_TRANSPARENCY \"SDL_VIDEO_EGL_ALLOW_TRANSPARENCY\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_EGL_ALLOW_TRANSPARENCY => "SDL_VIDEO_EGL_ALLOW_TRANSPARENCY"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_EXTERNAL_CONTEXT \"SDL_VIDEO_EXTERNAL_CONTEXT\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_EXTERNAL_CONTEXT => "SDL_VIDEO_EXTERNAL_CONTEXT"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_HIGHDPI_DISABLED \"SDL_VIDEO_HIGHDPI_DISABLED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_HIGHDPI_DISABLED => "SDL_VIDEO_HIGHDPI_DISABLED"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_MAC_FULLSCREEN_SPACES \"SDL_VIDEO_MAC_FULLSCREEN_SPACES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_MAC_FULLSCREEN_SPACES => "SDL_VIDEO_MAC_FULLSCREEN_SPACES"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_MINIMIZE_ON_FOCUS_LOSS \"SDL_VIDEO_MINIMIZE_ON_FOCUS_LOSS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_MINIMIZE_ON_FOCUS_LOSS => "SDL_VIDEO_MINIMIZE_ON_FOCUS_LOSS"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_WAYLAND_ALLOW_LIBDECOR \"SDL_VIDEO_WAYLAND_ALLOW_LIBDECOR\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_WAYLAND_ALLOW_LIBDECOR => "SDL_VIDEO_WAYLAND_ALLOW_LIBDECOR"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_WAYLAND_PREFER_LIBDECOR \"SDL_VIDEO_WAYLAND_PREFER_LIBDECOR\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_WAYLAND_PREFER_LIBDECOR => "SDL_VIDEO_WAYLAND_PREFER_LIBDECOR"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_WAYLAND_MODE_EMULATION \"SDL_VIDEO_WAYLAND_MODE_EMULATION\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_WAYLAND_MODE_EMULATION => "SDL_VIDEO_WAYLAND_MODE_EMULATION"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_WAYLAND_EMULATE_MOUSE_WARP \"SDL_VIDEO_WAYLAND_EMULATE_MOUSE_WARP\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_WAYLAND_EMULATE_MOUSE_WARP => "SDL_VIDEO_WAYLAND_EMULATE_MOUSE_WARP"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_WINDOW_SHARE_PIXEL_FORMAT \"SDL_VIDEO_WINDOW_SHARE_PIXEL_FORMAT\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_WINDOW_SHARE_PIXEL_FORMAT => "SDL_VIDEO_WINDOW_SHARE_PIXEL_FORMAT"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_FOREIGN_WINDOW_OPENGL \"SDL_VIDEO_FOREIGN_WINDOW_OPENGL\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_FOREIGN_WINDOW_OPENGL => "SDL_VIDEO_FOREIGN_WINDOW_OPENGL"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_FOREIGN_WINDOW_VULKAN \"SDL_VIDEO_FOREIGN_WINDOW_VULKAN\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_FOREIGN_WINDOW_VULKAN => "SDL_VIDEO_FOREIGN_WINDOW_VULKAN"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_WIN_D3DCOMPILER \"SDL_VIDEO_WIN_D3DCOMPILER\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_WIN_D3DCOMPILER => "SDL_VIDEO_WIN_D3DCOMPILER"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_X11_FORCE_EGL \"SDL_VIDEO_X11_FORCE_EGL\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_X11_FORCE_EGL => "SDL_VIDEO_X11_FORCE_EGL"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_X11_NET_WM_BYPASS_COMPOSITOR \"SDL_VIDEO_X11_NET_WM_BYPASS_COMPOSITOR\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_X11_NET_WM_BYPASS_COMPOSITOR => "SDL_VIDEO_X11_NET_WM_BYPASS_COMPOSITOR"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_X11_NET_WM_PING \"SDL_VIDEO_X11_NET_WM_PING\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_X11_NET_WM_PING => "SDL_VIDEO_X11_NET_WM_PING"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_X11_WINDOW_VISUALID \"SDL_VIDEO_X11_WINDOW_VISUALID\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_X11_WINDOW_VISUALID => "SDL_VIDEO_X11_WINDOW_VISUALID"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_X11_XINERAMA \"SDL_VIDEO_X11_XINERAMA\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_X11_XINERAMA => "SDL_VIDEO_X11_XINERAMA"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_X11_XRANDR \"SDL_VIDEO_X11_XRANDR\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_X11_XRANDR => "SDL_VIDEO_X11_XRANDR"u8;

        [NativeTypeName("#define SDL_HINT_VIDEO_X11_XVIDMODE \"SDL_VIDEO_X11_XVIDMODE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEO_X11_XVIDMODE => "SDL_VIDEO_X11_XVIDMODE"u8;

        [NativeTypeName("#define SDL_HINT_WAVE_FACT_CHUNK \"SDL_WAVE_FACT_CHUNK\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WAVE_FACT_CHUNK => "SDL_WAVE_FACT_CHUNK"u8;

        [NativeTypeName("#define SDL_HINT_WAVE_RIFF_CHUNK_SIZE \"SDL_WAVE_RIFF_CHUNK_SIZE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WAVE_RIFF_CHUNK_SIZE => "SDL_WAVE_RIFF_CHUNK_SIZE"u8;

        [NativeTypeName("#define SDL_HINT_WAVE_TRUNCATION \"SDL_WAVE_TRUNCATION\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WAVE_TRUNCATION => "SDL_WAVE_TRUNCATION"u8;

        [NativeTypeName("#define SDL_HINT_WINDOWS_DISABLE_THREAD_NAMING \"SDL_WINDOWS_DISABLE_THREAD_NAMING\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOWS_DISABLE_THREAD_NAMING => "SDL_WINDOWS_DISABLE_THREAD_NAMING"u8;

        [NativeTypeName("#define SDL_HINT_WINDOWS_ENABLE_MENU_MNEMONICS \"SDL_WINDOWS_ENABLE_MENU_MNEMONICS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOWS_ENABLE_MENU_MNEMONICS => "SDL_WINDOWS_ENABLE_MENU_MNEMONICS"u8;

        [NativeTypeName("#define SDL_HINT_WINDOWS_ENABLE_MESSAGELOOP \"SDL_WINDOWS_ENABLE_MESSAGELOOP\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOWS_ENABLE_MESSAGELOOP => "SDL_WINDOWS_ENABLE_MESSAGELOOP"u8;

        [NativeTypeName("#define SDL_HINT_WINDOWS_FORCE_MUTEX_CRITICAL_SECTIONS \"SDL_WINDOWS_FORCE_MUTEX_CRITICAL_SECTIONS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOWS_FORCE_MUTEX_CRITICAL_SECTIONS => "SDL_WINDOWS_FORCE_MUTEX_CRITICAL_SECTIONS"u8;

        [NativeTypeName("#define SDL_HINT_WINDOWS_FORCE_SEMAPHORE_KERNEL \"SDL_WINDOWS_FORCE_SEMAPHORE_KERNEL\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOWS_FORCE_SEMAPHORE_KERNEL => "SDL_WINDOWS_FORCE_SEMAPHORE_KERNEL"u8;

        [NativeTypeName("#define SDL_HINT_WINDOWS_INTRESOURCE_ICON \"SDL_WINDOWS_INTRESOURCE_ICON\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOWS_INTRESOURCE_ICON => "SDL_WINDOWS_INTRESOURCE_ICON"u8;

        [NativeTypeName("#define SDL_HINT_WINDOWS_INTRESOURCE_ICON_SMALL \"SDL_WINDOWS_INTRESOURCE_ICON_SMALL\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOWS_INTRESOURCE_ICON_SMALL => "SDL_WINDOWS_INTRESOURCE_ICON_SMALL"u8;

        [NativeTypeName("#define SDL_HINT_WINDOWS_NO_CLOSE_ON_ALT_F4 \"SDL_WINDOWS_NO_CLOSE_ON_ALT_F4\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOWS_NO_CLOSE_ON_ALT_F4 => "SDL_WINDOWS_NO_CLOSE_ON_ALT_F4"u8;

        [NativeTypeName("#define SDL_HINT_WINDOWS_USE_D3D9EX \"SDL_WINDOWS_USE_D3D9EX\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOWS_USE_D3D9EX => "SDL_WINDOWS_USE_D3D9EX"u8;

        [NativeTypeName("#define SDL_HINT_WINDOWS_DPI_AWARENESS \"SDL_WINDOWS_DPI_AWARENESS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOWS_DPI_AWARENESS => "SDL_WINDOWS_DPI_AWARENESS"u8;

        [NativeTypeName("#define SDL_HINT_WINDOWS_DPI_SCALING \"SDL_WINDOWS_DPI_SCALING\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOWS_DPI_SCALING => "SDL_WINDOWS_DPI_SCALING"u8;

        [NativeTypeName("#define SDL_HINT_WINDOW_FRAME_USABLE_WHILE_CURSOR_HIDDEN \"SDL_WINDOW_FRAME_USABLE_WHILE_CURSOR_HIDDEN\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOW_FRAME_USABLE_WHILE_CURSOR_HIDDEN => "SDL_WINDOW_FRAME_USABLE_WHILE_CURSOR_HIDDEN"u8;

        [NativeTypeName("#define SDL_HINT_WINDOW_NO_ACTIVATION_WHEN_SHOWN \"SDL_WINDOW_NO_ACTIVATION_WHEN_SHOWN\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINDOW_NO_ACTIVATION_WHEN_SHOWN => "SDL_WINDOW_NO_ACTIVATION_WHEN_SHOWN"u8;

        [NativeTypeName("#define SDL_HINT_WINRT_HANDLE_BACK_BUTTON \"SDL_WINRT_HANDLE_BACK_BUTTON\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINRT_HANDLE_BACK_BUTTON => "SDL_WINRT_HANDLE_BACK_BUTTON"u8;

        [NativeTypeName("#define SDL_HINT_WINRT_PRIVACY_POLICY_LABEL \"SDL_WINRT_PRIVACY_POLICY_LABEL\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINRT_PRIVACY_POLICY_LABEL => "SDL_WINRT_PRIVACY_POLICY_LABEL"u8;

        [NativeTypeName("#define SDL_HINT_WINRT_PRIVACY_POLICY_URL \"SDL_WINRT_PRIVACY_POLICY_URL\"")]
        public static ReadOnlySpan<byte> SDL_HINT_WINRT_PRIVACY_POLICY_URL => "SDL_WINRT_PRIVACY_POLICY_URL"u8;

        [NativeTypeName("#define SDL_HINT_X11_FORCE_OVERRIDE_REDIRECT \"SDL_X11_FORCE_OVERRIDE_REDIRECT\"")]
        public static ReadOnlySpan<byte> SDL_HINT_X11_FORCE_OVERRIDE_REDIRECT => "SDL_X11_FORCE_OVERRIDE_REDIRECT"u8;

        [NativeTypeName("#define SDL_HINT_XINPUT_ENABLED \"SDL_XINPUT_ENABLED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_XINPUT_ENABLED => "SDL_XINPUT_ENABLED"u8;

        [NativeTypeName("#define SDL_HINT_DIRECTINPUT_ENABLED \"SDL_DIRECTINPUT_ENABLED\"")]
        public static ReadOnlySpan<byte> SDL_HINT_DIRECTINPUT_ENABLED => "SDL_DIRECTINPUT_ENABLED"u8;

        [NativeTypeName("#define SDL_HINT_XINPUT_USE_OLD_JOYSTICK_MAPPING \"SDL_XINPUT_USE_OLD_JOYSTICK_MAPPING\"")]
        public static ReadOnlySpan<byte> SDL_HINT_XINPUT_USE_OLD_JOYSTICK_MAPPING => "SDL_XINPUT_USE_OLD_JOYSTICK_MAPPING"u8;

        [NativeTypeName("#define SDL_HINT_AUDIO_INCLUDE_MONITORS \"SDL_AUDIO_INCLUDE_MONITORS\"")]
        public static ReadOnlySpan<byte> SDL_HINT_AUDIO_INCLUDE_MONITORS => "SDL_AUDIO_INCLUDE_MONITORS"u8;

        [NativeTypeName("#define SDL_HINT_X11_WINDOW_TYPE \"SDL_X11_WINDOW_TYPE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_X11_WINDOW_TYPE => "SDL_X11_WINDOW_TYPE"u8;

        [NativeTypeName("#define SDL_HINT_QUIT_ON_LAST_WINDOW_CLOSE \"SDL_QUIT_ON_LAST_WINDOW_CLOSE\"")]
        public static ReadOnlySpan<byte> SDL_HINT_QUIT_ON_LAST_WINDOW_CLOSE => "SDL_QUIT_ON_LAST_WINDOW_CLOSE"u8;

        [NativeTypeName("#define SDL_HINT_VIDEODRIVER \"SDL_VIDEODRIVER\"")]
        public static ReadOnlySpan<byte> SDL_HINT_VIDEODRIVER => "SDL_VIDEODRIVER"u8;

        [NativeTypeName("#define SDL_HINT_AUDIODRIVER \"SDL_AUDIODRIVER\"")]
        public static ReadOnlySpan<byte> SDL_HINT_AUDIODRIVER => "SDL_AUDIODRIVER"u8;

        [NativeTypeName("#define SDL_HINT_KMSDRM_DEVICE_INDEX \"SDL_KMSDRM_DEVICE_INDEX\"")]
        public static ReadOnlySpan<byte> SDL_HINT_KMSDRM_DEVICE_INDEX => "SDL_KMSDRM_DEVICE_INDEX"u8;

        [NativeTypeName("#define SDL_HINT_TRACKPAD_IS_TOUCH_ONLY \"SDL_TRACKPAD_IS_TOUCH_ONLY\"")]
        public static ReadOnlySpan<byte> SDL_HINT_TRACKPAD_IS_TOUCH_ONLY => "SDL_TRACKPAD_IS_TOUCH_ONLY"u8;

        [NativeTypeName("#define SDL_HINT_SHUTDOWN_DBUS_ON_QUIT \"SDL_SHUTDOWN_DBUS_ON_QUIT\"")]
        public static ReadOnlySpan<byte> SDL_HINT_SHUTDOWN_DBUS_ON_QUIT => "SDL_SHUTDOWN_DBUS_ON_QUIT"u8;

        [NativeTypeName("#define SDL_HINT_APPLE_RWFROMFILE_USE_RESOURCES \"SDL_APPLE_RWFROMFILE_USE_RESOURCES\"")]
        public static ReadOnlySpan<byte> SDL_HINT_APPLE_RWFROMFILE_USE_RESOURCES => "SDL_APPLE_RWFROMFILE_USE_RESOURCES"u8;
    }
}
