# Oracle Comparison — ClangSharp Spike vs Cake-generated Preview

**Date:** 2026-05-22
**Spike output:** `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/`
**Oracle:** `artifacts/generated-bindings-preview/sdl2-core/`
**Dynapi exports source:** `external\vcpkg\buildtrees\sdl2\src\se-2.32.10-3b143ac573.clean\src\dynapi\SDL2.exports`

## Why this is not a 1:1 comparison

- ClangSharp emits per-header (each `.g.cs` mixes functions, enums, structs, constants for that header).
- Cake emits per-category (`Enums.g.cs`, `Structs.g.cs`, `Handles.g.cs`, `Callbacks.g.cs`, `Constants.g.cs`, per-platform `Commands.g.cs`).
- Cake distinguishes opaque handles from POD structs; the spike has to infer it from empty struct bodies.
- Cake emits named callback delegates (`SDL_AudioCallback` etc.); ClangSharp emits inline `delegate* unmanaged[Cdecl]<...>` at every use site — the inline count is informational only.
- Cake's macro pipeline emits computed constants (FOURCC literals etc.) that ClangSharp may inline at the use site rather than as standalone constants; expect spike `Constants` to skew low or differently-shaped.

## Summary

| Category | Spike | Oracle | Delta |
| --- | ---: | ---: | ---: |
| Functions | 859 | 866 | -7 |
| Enums | 57 | 56 | +1 |
| POD structs | 96 | 71 | +25 |
| Opaque handles | 24 | 16 | +8 |
| Constants | 149 | 104 | +45 |
| Callbacks | 58 | 19 | +39 |

## Dynapi Coherence

- Dynapi exports: **845**
- Spike functions: **859**
- In dynapi AND emitted: **831**
- In dynapi but NOT emitted: **14**
- Emitted but NOT in dynapi: **28**

<details><summary>In dynapi, missing from spike (14)</summary>

```text
SDL_CreateThread
SDL_CreateThreadWithStackSize
SDL_DYNAPI_entry
SDL_GetWindowWMInfo
SDL_Init
SDL_InitSubSystem
SDL_LogMessageV
SDL_Quit
SDL_QuitSubSystem
SDL_RWFromFP
SDL_WasInit
SDL_vasprintf
SDL_vsnprintf
SDL_vsscanf
```

</details>

<details><summary>Emitted but not in dynapi (28)</summary>

```text
SDL_AndroidBackButton
SDL_AndroidGetActivity
SDL_AndroidGetExternalStoragePath
SDL_AndroidGetExternalStorageState
SDL_AndroidGetInternalStoragePath
SDL_AndroidGetJNIEnv
SDL_AndroidRequestPermission
SDL_AndroidSendMessage
SDL_AndroidShowToast
SDL_GDKGetDefaultUser
SDL_GDKGetTaskQueue
SDL_GDKRunApp
SDL_GDKSuspendComplete
SDL_GetAndroidSDKVersion
SDL_IsAndroidTV
SDL_IsChromebook
SDL_IsDeXMode
SDL_LinuxSetThreadPriority
SDL_LinuxSetThreadPriorityAndPolicy
SDL_OnApplicationDidChangeStatusBarOrientation
SDL_UIKitRunApp
SDL_WinRTGetDeviceFamily
SDL_WinRTGetFSPathUNICODE
SDL_WinRTGetFSPathUTF8
SDL_WinRTRunApp
SDL_iPhoneSetAnimationCallback
SDL_iPhoneSetEventPump
SDL_main
```

</details>

## Per-Category Set Diffs

### Functions

- Spike: **859**
- Oracle: **866**
- Common: **858**
- Spike-only: **1**
- Oracle-only: **8**

<details><summary>Spike-only (1)</summary>

```text
SDL_main
```

</details>

<details><summary>Oracle-only (8)</summary>

```text
SDL_CreateThread
SDL_CreateThreadWithStackSize
SDL_GetWindowWMInfo
SDL_Init
SDL_InitSubSystem
SDL_Quit
SDL_QuitSubSystem
SDL_WasInit
```

</details>

### Enums

- Spike: **57**
- Oracle: **56**
- Common: **56**
- Spike-only: **1**
- Oracle-only: **0**

<details><summary>Spike-only (1)</summary>

```text
WindowShapeMode
```

</details>

### POD structs

- Spike: **96**
- Oracle: **71**
- Common: **67**
- Spike-only: **29**
- Oracle-only: **4**

<details><summary>Spike-only (29)</summary>

```text
SDL_GUID
SDL_RWops
SDL_SysWMinfo
SDL_SysWMmsg
_buffer_e__Struct
_center_e__FixedBuffer
_colors_e__FixedBuffer
_data_e__FixedBuffer
_deadband_e__FixedBuffer
_dir_e__FixedBuffer
_dummy_e__FixedBuffer
_filters_e__FixedBuffer
_hat_e__Struct
_hidden_e__Union
_info_e__Union
_left_coeff_e__FixedBuffer
_left_sat_e__FixedBuffer
_mem_e__Struct
_msg_e__Union
_padding_e__FixedBuffer
_right_coeff_e__FixedBuffer
_right_sat_e__FixedBuffer
_stdio_e__Struct
_text_e__FixedBuffer
_texture_formats_e__FixedBuffer
_unknown_e__Struct
_value_e__Union
_win_e__Struct
_windowsio_e__Struct
```

</details>

<details><summary>Oracle-only (4)</summary>

```text
SDL_AudioCVT_filters
SDL_GameControllerButtonBind_value
SDL_GameControllerButtonBind_value_hat
SDL_MessageBoxColorScheme_colors
```

</details>

### Opaque handles

- Spike: **24**
- Oracle: **16**
- Common: **8**
- Spike-only: **16**
- Oracle-only: **8**

<details><summary>Spike-only (16)</summary>

```text
ID3D11Device
ID3D12Device
IDirect3DDevice9
SDL_SysWMmsg
SDL_hid_device_
SDL_semaphore
VkInstance_T
VkSurfaceKHR_T
XTaskQueueObject
XUser
_SDL_AudioStream
_SDL_GameController
_SDL_Haptic
_SDL_Joystick
_SDL_Sensor
_SDL_iconv_t
```

</details>

<details><summary>Oracle-only (8)</summary>

```text
SDL_AudioStream
SDL_GameController
SDL_Haptic
SDL_Joystick
SDL_RWops
SDL_Sensor
SDL_hid_device
SDL_sem
```

</details>

### Constants

- Spike: **149**
- Oracle: **104**
- Common: **94**
- Spike-only: **55**
- Oracle-only: **10**

<details><summary>Spike-only (55)</summary>

```text
AUDIO_F32
AUDIO_F32LSB
AUDIO_F32MSB
AUDIO_F32SYS
AUDIO_S16
AUDIO_S16LSB
AUDIO_S16MSB
AUDIO_S16SYS
AUDIO_S32
AUDIO_S32LSB
AUDIO_S32MSB
AUDIO_S32SYS
AUDIO_S8
AUDIO_U16
AUDIO_U16LSB
AUDIO_U16MSB
AUDIO_U16SYS
AUDIO_U8
HAVE_WINAPIFAMILY_H
RW_SEEK_CUR
RW_SEEK_END
RW_SEEK_SET
SDLK_SCANCODE_MASK
SDL_COMPILEDVERSION
SDL_FLT_EPSILON
SDL_ICONV_E2BIG
SDL_ICONV_EILSEQ
SDL_ICONV_EINVAL
SDL_ICONV_ERROR
SDL_IPHONE_MAX_GFORCE
SDL_MAX_SINT16
SDL_MAX_SINT32
SDL_MAX_SINT64
SDL_MAX_SINT8
SDL_MAX_UINT16
SDL_MAX_UINT32
SDL_MAX_UINT64
SDL_MAX_UINT8
SDL_MIN_SINT16
SDL_MIN_SINT32
SDL_MIN_SINT64
SDL_MIN_SINT8
SDL_MIN_UINT16
SDL_MIN_UINT32
SDL_MIN_UINT64
SDL_MIN_UINT8
SDL_MOUSE_TOUCHID
SDL_MUTEX_MAXWAIT
SDL_REVISION_NUMBER
SDL_STANDARD_GRAVITY
SDL_TOUCH_MOUSEID
SDL_WINAPI_FAMILY_PHONE
WINAPI_FAMILY_WINRT
__WIN32__
__WINDOWS__
```

</details>

<details><summary>Oracle-only (10)</summary>

```text
SDL_INIT_AUDIO
SDL_INIT_EVENTS
SDL_INIT_EVERYTHING
SDL_INIT_GAMECONTROLLER
SDL_INIT_HAPTIC
SDL_INIT_JOYSTICK
SDL_INIT_NOPARACHUTE
SDL_INIT_SENSOR
SDL_INIT_TIMER
SDL_INIT_VIDEO
```

</details>

