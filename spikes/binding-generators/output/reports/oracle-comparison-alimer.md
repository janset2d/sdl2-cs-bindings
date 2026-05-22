# Oracle Comparison — Alimer-Style CppAst Spike vs Cake-generated Preview

**Date:** 2026-05-21
**Spike output:** `spikes/binding-generators/output/alimer/Generated/`
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
| Functions | 846 | 866 | -20 |
| Enums | 0 | 56 | -56 |
| POD structs | 0 | 71 | -71 |
| Opaque handles | 0 | 16 | -16 |
| Constants | 0 | 104 | -104 |
| Callbacks | 0 | 19 | -19 |

## Dynapi Coherence

- Dynapi exports: **845**
- Spike functions: **846**
- In dynapi AND emitted: **781**
- In dynapi but NOT emitted: **64**
- Emitted but NOT in dynapi: **65**

<details><summary>In dynapi, missing from spike (64)</summary>

```text
SDL_AddEventWatch
SDL_AddHintCallback
SDL_AddTimer
SDL_AtomicCASPtr
SDL_AtomicGetPtr
SDL_AtomicSetPtr
SDL_CreateThread
SDL_CreateThreadWithStackSize
SDL_CreateWindowAndRenderer
SDL_DYNAPI_entry
SDL_DelEventWatch
SDL_DelHintCallback
SDL_FilterEvents
SDL_GUIDFromString
SDL_GUIDToString
SDL_GameControllerGetBindForAxis
SDL_GameControllerGetBindForButton
SDL_GetAssertionHandler
SDL_GetDefaultAssertionHandler
SDL_GetDefaultAudioInfo
SDL_GetEventFilter
SDL_GetRenderDrawBlendMode
SDL_GetSurfaceBlendMode
SDL_GetTextureBlendMode
SDL_GetTextureScaleMode
SDL_GetWindowWMInfo
SDL_Init
SDL_InitSubSystem
SDL_LoadWAV_RW
SDL_LockTexture
SDL_LockTextureToSurface
SDL_LogGetOutputFunction
SDL_LogMessageV
SDL_LogSetOutputFunction
SDL_Quit
SDL_QuitSubSystem
SDL_RWFromFP
SDL_RenderGetD3D11Device
SDL_RenderGetD3D12Device
SDL_RenderGetD3D9Device
SDL_SetAssertionHandler
SDL_SetEventFilter
SDL_SetMemoryFunctions
SDL_SetWindowHitTest
SDL_SetWindowsMessageHook
SDL_TLSSet
SDL_Vulkan_CreateSurface
SDL_Vulkan_GetInstanceExtensions
SDL_WasInit
SDL_asprintf
SDL_bsearch
SDL_iconv
SDL_iconv_close
SDL_iconv_open
SDL_qsort
SDL_strtod
SDL_strtokr
SDL_strtol
SDL_strtoll
SDL_strtoul
... (4 more)
```

</details>

<details><summary>Emitted but not in dynapi (65)</summary>

```text
IMG_Init
IMG_Linked_Version
IMG_Load
IMG_LoadAVIF_RW
IMG_LoadBMP_RW
IMG_LoadCUR_RW
IMG_LoadGIF_RW
IMG_LoadICO_RW
IMG_LoadJPG_RW
IMG_LoadJXL_RW
IMG_LoadLBM_RW
IMG_LoadPCX_RW
IMG_LoadPNG_RW
IMG_LoadPNM_RW
IMG_LoadQOI_RW
IMG_LoadSVG_RW
IMG_LoadSizedSVG_RW
IMG_LoadTGA_RW
IMG_LoadTIF_RW
IMG_LoadTexture
IMG_LoadTextureTyped_RW
IMG_LoadTexture_RW
IMG_LoadTyped_RW
IMG_LoadWEBP_RW
IMG_LoadXCF_RW
IMG_LoadXPM_RW
IMG_LoadXV_RW
IMG_Load_RW
IMG_Quit
IMG_SaveJPG
IMG_SaveJPG_RW
IMG_SavePNG
IMG_SavePNG_RW
IMG_isAVIF
IMG_isBMP
IMG_isCUR
IMG_isGIF
IMG_isICO
IMG_isJPG
IMG_isJXL
IMG_isLBM
IMG_isPCX
IMG_isPNG
IMG_isPNM
IMG_isQOI
IMG_isSVG
IMG_isTIF
IMG_isWEBP
IMG_isXCF
IMG_isXPM
IMG_isXV
SDL_FRectEmpty
SDL_FRectEquals
SDL_FRectEqualsEpsilon
SDL_HasExactlyOneBitSet32
SDL_MostSignificantBitIndex32
SDL_PointInFRect
SDL_PointInRect
SDL_RectEmpty
SDL_RectEquals
... (5 more)
```

</details>

## Per-Category Set Diffs

### Functions

- Spike: **846**
- Oracle: **866**
- Common: **781**
- Spike-only: **65**
- Oracle-only: **85**

<details><summary>Spike-only (65)</summary>

```text
IMG_Init
IMG_Linked_Version
IMG_Load
IMG_LoadAVIF_RW
IMG_LoadBMP_RW
IMG_LoadCUR_RW
IMG_LoadGIF_RW
IMG_LoadICO_RW
IMG_LoadJPG_RW
IMG_LoadJXL_RW
IMG_LoadLBM_RW
IMG_LoadPCX_RW
IMG_LoadPNG_RW
IMG_LoadPNM_RW
IMG_LoadQOI_RW
IMG_LoadSVG_RW
IMG_LoadSizedSVG_RW
IMG_LoadTGA_RW
IMG_LoadTIF_RW
IMG_LoadTexture
IMG_LoadTextureTyped_RW
IMG_LoadTexture_RW
IMG_LoadTyped_RW
IMG_LoadWEBP_RW
IMG_LoadXCF_RW
IMG_LoadXPM_RW
IMG_LoadXV_RW
IMG_Load_RW
IMG_Quit
IMG_SaveJPG
IMG_SaveJPG_RW
IMG_SavePNG
IMG_SavePNG_RW
IMG_isAVIF
IMG_isBMP
IMG_isCUR
IMG_isGIF
IMG_isICO
IMG_isJPG
IMG_isJXL
IMG_isLBM
IMG_isPCX
IMG_isPNG
IMG_isPNM
IMG_isQOI
IMG_isSVG
IMG_isTIF
IMG_isWEBP
IMG_isXCF
IMG_isXPM
... (15 more)
```

</details>

<details><summary>Oracle-only (85)</summary>

```text
SDL_AddEventWatch
SDL_AddHintCallback
SDL_AddTimer
SDL_AndroidBackButton
SDL_AndroidGetActivity
SDL_AndroidGetExternalStoragePath
SDL_AndroidGetExternalStorageState
SDL_AndroidGetInternalStoragePath
SDL_AndroidGetJNIEnv
SDL_AndroidRequestPermission
SDL_AndroidSendMessage
SDL_AndroidShowToast
SDL_AtomicCASPtr
SDL_AtomicGetPtr
SDL_AtomicSetPtr
SDL_CreateThread
SDL_CreateThreadWithStackSize
SDL_CreateWindowAndRenderer
SDL_DelEventWatch
SDL_DelHintCallback
SDL_FilterEvents
SDL_GDKGetDefaultUser
SDL_GDKGetTaskQueue
SDL_GDKRunApp
SDL_GDKSuspendComplete
SDL_GUIDFromString
SDL_GUIDToString
SDL_GameControllerGetBindForAxis
SDL_GameControllerGetBindForButton
SDL_GetAndroidSDKVersion
SDL_GetAssertionHandler
SDL_GetDefaultAssertionHandler
SDL_GetDefaultAudioInfo
SDL_GetEventFilter
SDL_GetRenderDrawBlendMode
SDL_GetSurfaceBlendMode
SDL_GetTextureBlendMode
SDL_GetTextureScaleMode
SDL_GetWindowWMInfo
SDL_Init
SDL_InitSubSystem
SDL_IsAndroidTV
SDL_IsChromebook
SDL_IsDeXMode
SDL_LinuxSetThreadPriority
SDL_LinuxSetThreadPriorityAndPolicy
SDL_LoadWAV_RW
SDL_LockTexture
SDL_LockTextureToSurface
SDL_LogGetOutputFunction
... (35 more)
```

</details>

### Enums

- Spike: **0**
- Oracle: **56**
- Common: **0**
- Spike-only: **0**
- Oracle-only: **56**

<details><summary>Oracle-only (56)</summary>

```text
SDL_ArrayOrder
SDL_AssertState
SDL_AudioStatus
SDL_BitmapOrder
SDL_BlendFactor
SDL_BlendMode
SDL_BlendOperation
SDL_DisplayEventID
SDL_DisplayOrientation
SDL_EventType
SDL_FlashOperation
SDL_GLContextResetNotification
SDL_GLattr
SDL_GLcontextFlag
SDL_GLcontextReleaseFlag
SDL_GLprofile
SDL_GameControllerAxis
SDL_GameControllerBindType
SDL_GameControllerButton
SDL_GameControllerType
SDL_HintPriority
SDL_HitTestResult
SDL_JoystickPowerLevel
SDL_JoystickType
SDL_KeyCode
SDL_Keymod
SDL_LogCategory
SDL_LogPriority
SDL_MessageBoxButtonFlags
SDL_MessageBoxColorType
SDL_MessageBoxFlags
SDL_MouseWheelDirection
SDL_PackedLayout
SDL_PackedOrder
SDL_PixelFormatEnum
SDL_PixelType
SDL_PowerState
SDL_RendererFlags
SDL_RendererFlip
SDL_SYSWM_TYPE
... (16 more)
```

</details>

### POD structs

- Spike: **0**
- Oracle: **71**
- Common: **0**
- Spike-only: **0**
- Oracle-only: **71**

<details><summary>Oracle-only (71)</summary>

```text
SDL_AssertData
SDL_AudioCVT
SDL_AudioCVT_filters
SDL_AudioDeviceEvent
SDL_AudioSpec
SDL_Color
SDL_CommonEvent
SDL_ControllerAxisEvent
SDL_ControllerButtonEvent
SDL_ControllerDeviceEvent
SDL_ControllerSensorEvent
SDL_ControllerTouchpadEvent
SDL_DisplayEvent
SDL_DisplayMode
SDL_DollarGestureEvent
SDL_DropEvent
SDL_Event
SDL_FPoint
SDL_FRect
SDL_Finger
SDL_GameControllerButtonBind
SDL_GameControllerButtonBind_value
SDL_GameControllerButtonBind_value_hat
SDL_HapticCondition
SDL_HapticConstant
SDL_HapticCustom
SDL_HapticDirection
SDL_HapticEffect
SDL_HapticLeftRight
SDL_HapticPeriodic
SDL_HapticRamp
SDL_JoyAxisEvent
SDL_JoyBallEvent
SDL_JoyBatteryEvent
SDL_JoyButtonEvent
SDL_JoyDeviceEvent
SDL_JoyHatEvent
SDL_KeyboardEvent
SDL_Keysym
SDL_Locale
... (31 more)
```

</details>

### Opaque handles

- Spike: **0**
- Oracle: **16**
- Common: **0**
- Spike-only: **0**
- Oracle-only: **16**

<details><summary>Oracle-only (16)</summary>

```text
SDL_AudioStream
SDL_BlitMap
SDL_Cursor
SDL_GameController
SDL_Haptic
SDL_Joystick
SDL_RWops
SDL_Renderer
SDL_Sensor
SDL_Texture
SDL_Thread
SDL_Window
SDL_cond
SDL_hid_device
SDL_mutex
SDL_sem
```

</details>

### Constants

- Spike: **0**
- Oracle: **104**
- Common: **0**
- Spike-only: **0**
- Oracle-only: **104**

<details><summary>Oracle-only (104)</summary>

```text
SDL_ALPHA_OPAQUE
SDL_ALPHA_TRANSPARENT
SDL_ANDROID_EXTERNAL_STORAGE_READ
SDL_ANDROID_EXTERNAL_STORAGE_WRITE
SDL_AUDIOCVT_MAX_FILTERS
SDL_AUDIO_ALLOW_ANY_CHANGE
SDL_AUDIO_ALLOW_CHANNELS_CHANGE
SDL_AUDIO_ALLOW_FORMAT_CHANGE
SDL_AUDIO_ALLOW_FREQUENCY_CHANGE
SDL_AUDIO_ALLOW_SAMPLES_CHANGE
SDL_AUDIO_MASK_BITSIZE
SDL_AUDIO_MASK_DATATYPE
SDL_AUDIO_MASK_ENDIAN
SDL_AUDIO_MASK_SIGNED
SDL_BIG_ENDIAN
SDL_BUTTON_LEFT
SDL_BUTTON_LMASK
SDL_BUTTON_MIDDLE
SDL_BUTTON_MMASK
SDL_BUTTON_RIGHT
SDL_BUTTON_RMASK
SDL_BUTTON_X1
SDL_BUTTON_X1MASK
SDL_BUTTON_X2
SDL_BUTTON_X2MASK
SDL_BYTEORDER
SDL_DISABLE
SDL_DONTFREE
SDL_ENABLE
SDL_FLOATWORDORDER
SDL_HAPTIC_AUTOCENTER
SDL_HAPTIC_CARTESIAN
SDL_HAPTIC_CONSTANT
SDL_HAPTIC_CUSTOM
SDL_HAPTIC_DAMPER
SDL_HAPTIC_FRICTION
SDL_HAPTIC_GAIN
SDL_HAPTIC_INERTIA
SDL_HAPTIC_INFINITY
SDL_HAPTIC_LEFTRIGHT
SDL_HAPTIC_PAUSE
SDL_HAPTIC_POLAR
SDL_HAPTIC_RAMP
SDL_HAPTIC_SAWTOOTHDOWN
SDL_HAPTIC_SAWTOOTHUP
SDL_HAPTIC_SINE
SDL_HAPTIC_SPHERICAL
SDL_HAPTIC_SPRING
SDL_HAPTIC_STATUS
SDL_HAPTIC_STEERING_AXIS
SDL_HAPTIC_TRIANGLE
SDL_HAT_CENTERED
SDL_HAT_DOWN
SDL_HAT_LEFT
SDL_HAT_LEFTDOWN
SDL_HAT_LEFTUP
SDL_HAT_RIGHT
SDL_HAT_RIGHTDOWN
SDL_HAT_RIGHTUP
SDL_HAT_UP
SDL_IGNORE
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
SDL_INVALID_SHAPE_ARGUMENT
SDL_JOYSTICK_AXIS_MAX
SDL_JOYSTICK_AXIS_MIN
SDL_LIL_ENDIAN
SDL_MAJOR_VERSION
SDL_MAX_LOG_MESSAGE
SDL_METALVIEW_TAG
SDL_MINOR_VERSION
SDL_MIX_MAXVOLUME
... (24 more)
```

</details>

