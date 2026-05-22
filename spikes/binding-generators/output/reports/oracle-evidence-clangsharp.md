# ClangSharp Oracle Evidence

This spike report is evidence, not a correctness certificate. Raw ABI constitution checks are listed even when the generated code builds cleanly.

## Inputs

| Family | Source | Path | Status | Functions | Constants | Types |
| --- | --- | --- | --- | ---: | ---: | ---: |
| sdl2-core | Cake Preview | `artifacts/generated-bindings-preview/sdl2-core` | present | 854 | 351 | 152 |
| sdl2-core | ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat` | present | 871 | 362 | 224 |
| sdl2-core | ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern` | present | 871 | 362 | 240 |
| sdl2-core | SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | present | 872 | 0 | 0 |
| sdl2-core | Manifest Required Surface | `build/manifest.json` | present | 5 | 10 | 0 |
| sdl2-core | SDL2-CS | `external/sdl2-cs/src/SDL2.cs` | present | 789 | 292 | 131 |
| sdl2-image | Cake Preview | `n/a` | n/a | 0 | 0 | 0 |
| sdl2-image | ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Compat` | present | 59 | 4 | 3 |
| sdl2-image | ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern` | present | 59 | 4 | 3 |
| sdl2-image | SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| sdl2-image | Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| sdl2-image | SDL2-CS | `external/sdl2-cs/src/SDL2_image.cs` | present | 29 | 4 | 3 |

## Family: sdl2-core

- Display name: SDL2 Core
- Expected namespace: `SDL2`
- Expected raw class: `SDLNative`

### Source Status

| Source | Path | Status | Functions | Constants | Types |
| --- | --- | --- | ---: | ---: | ---: |
| Cake Preview | `artifacts/generated-bindings-preview/sdl2-core` | present | 854 | 351 | 152 |
| ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat` | present | 871 | 362 | 224 |
| ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern` | present | 871 | 362 | 240 |
| SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | present | 872 | 0 | 0 |
| Manifest Required Surface | `build/manifest.json` | present | 5 | 10 | 0 |
| SDL2-CS | `external/sdl2-cs/src/SDL2.cs` | present | 789 | 292 | 131 |

### Surface Counts

| Surface | Functions | Constants | Types |
| --- | ---: | ---: | ---: |
| Generated ClangSharp | 1742 | 724 | 464 |
| Cake Preview | 854 | 351 | 152 |
| SDL2-CS | 789 | 292 | 131 |
| Dynapi | 872 | 0 | 0 |
| Manifest Required Surface | 5 | 10 | 0 |

### Raw ABI Constitution Checks

#### Compatibility Risk

- Platform-sensitive scalar risks (`platform-sensitive-wchar`): 30 finding(s), 15 symbol(s).
  - Symbols:
  - `SDL_WinRTGetFSPathUNICODE`
  - `SDL_hid_get_indexed_string`
  - `SDL_hid_get_manufacturer_string`
  - `SDL_hid_get_product_string`
  - `SDL_hid_get_serial_number_string`
  - `SDL_hid_open`
  - `SDL_wcscasecmp`
  - `SDL_wcscmp`
  - `SDL_wcsdup`
  - `SDL_wcslcat`
  - `SDL_wcslcpy`
  - `SDL_wcslen`
  - `SDL_wcsncasecmp`
  - `SDL_wcsncmp`
  - `SDL_wcsstr`
  - Example: `SDL_WinRTGetFSPathUNICODE` - wchar_t is mapped to ushort*, which is platform-sensitive outside Windows. (`spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/Platforms/WinRT/SDL_system.g.cs`)
  - Example: `SDL_WinRTGetFSPathUNICODE` - wchar_t is mapped to ushort*, which is platform-sensitive outside Windows. (`spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/Platforms/WinRT/SDL_system.g.cs`)
  - Example: `SDL_hid_get_indexed_string` - wchar_t is mapped to ushort*, which is platform-sensitive outside Windows. (`spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_hidapi.g.cs`)
- Platform-sensitive scalar risks (`platform-sensitive-long`): 12 finding(s), 6 symbol(s).
  - Symbols:
  - `SDL_lround`
  - `SDL_lroundf`
  - `SDL_ltoa`
  - `SDL_strtol`
  - `SDL_strtoul`
  - `SDL_ultoa`
  - Example: `SDL_lround` - C long/unsigned long is mapped to int/uint, which is width-sensitive across supported platforms. (`spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_stdinc.g.cs`)
  - Example: `SDL_lround` - C long/unsigned long is mapped to int/uint, which is width-sensitive across supported platforms. (`spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_stdinc.g.cs`)
  - Example: `SDL_lroundf` - C long/unsigned long is mapped to int/uint, which is width-sensitive across supported platforms. (`spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_stdinc.g.cs`)

#### Hard Bug

- Deferred layout violations (`deferred-layout-sdl-rwops`): 2 finding(s), 1 symbol(s).
  - Symbols:
  - `SDL_RWops`
  - Example: `SDL_RWops` - SDL_RWops layout is deferred but ClangSharp emitted fields. (`spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_rwops.g.cs`)
  - Example: `SDL_RWops` - SDL_RWops layout is deferred but ClangSharp emitted fields. (`spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_rwops.g.cs`)
- Deferred layout violations (`deferred-layout-sdl-syswminfo`): 2 finding(s), 1 symbol(s).
  - Symbols:
  - `SDL_SysWMinfo`
  - Example: `SDL_SysWMinfo` - SDL_SysWMinfo layout is deferred but ClangSharp emitted fields. (`spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_syswm.g.cs`)
  - Example: `SDL_SysWMinfo` - SDL_SysWMinfo layout is deferred but ClangSharp emitted fields. (`spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_syswm.g.cs`)
- Deferred layout violations (`deferred-layout-sdl-syswmmsg`): 2 finding(s), 1 symbol(s).
  - Symbols:
  - `SDL_SysWMmsg`
  - Example: `SDL_SysWMmsg` - SDL_SysWMmsg layout is deferred but ClangSharp emitted fields. (`spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_syswm.g.cs`)
  - Example: `SDL_SysWMmsg` - SDL_SysWMmsg layout is deferred but ClangSharp emitted fields. (`spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_syswm.g.cs`)

### Function Matrix

| Function | Generated | Cake | SDL2-CS | Dynapi |
| --- | --- | --- | --- | --- |
| `SDL_AddEventWatch` | yes | yes | yes | yes |
| `SDL_AddHintCallback` | yes | yes | no | yes |
| `SDL_AddTimer` | yes | yes | yes | yes |
| `SDL_AllocFormat` | yes | yes | yes | yes |
| `SDL_AllocPalette` | yes | yes | yes | yes |
| `SDL_AllocRW` | yes | yes | yes | yes |
| `SDL_AndroidBackButton` | yes | yes | yes | yes |
| `SDL_AndroidGetActivity` | yes | yes | yes | yes |
| `SDL_AndroidGetExternalStoragePath` | yes | yes | yes | yes |
| `SDL_AndroidGetExternalStorageState` | yes | yes | yes | yes |
| `SDL_AndroidGetInternalStoragePath` | yes | yes | yes | yes |
| `SDL_AndroidGetJNIEnv` | yes | yes | yes | yes |
| `SDL_AndroidRequestPermission` | yes | yes | yes | yes |
| `SDL_AndroidSendMessage` | yes | yes | no | yes |
| `SDL_AndroidShowToast` | yes | yes | yes | yes |
| `SDL_AtomicAdd` | yes | yes | no | yes |
| `SDL_AtomicCAS` | yes | yes | no | yes |
| `SDL_AtomicCASPtr` | yes | yes | no | yes |
| `SDL_AtomicGet` | yes | yes | no | yes |
| `SDL_AtomicGetPtr` | yes | yes | no | yes |
| `SDL_AtomicLock` | yes | yes | no | yes |
| `SDL_AtomicSet` | yes | yes | no | yes |
| `SDL_AtomicSetPtr` | yes | yes | no | yes |
| `SDL_AtomicTryLock` | yes | yes | no | yes |
| `SDL_AtomicUnlock` | yes | yes | no | yes |
| `SDL_AudioInit` | yes | yes | yes | yes |
| `SDL_AudioQuit` | yes | yes | yes | yes |
| `SDL_AudioStreamAvailable` | yes | yes | yes | yes |
| `SDL_AudioStreamClear` | yes | yes | yes | yes |
| `SDL_AudioStreamFlush` | yes | yes | no | yes |
| `SDL_AudioStreamGet` | yes | yes | yes | yes |
| `SDL_AudioStreamPut` | yes | yes | yes | yes |
| `SDL_BuildAudioCVT` | yes | yes | no | yes |
| `SDL_CalculateGammaRamp` | yes | yes | yes | yes |
| `SDL_CaptureMouse` | yes | yes | yes | yes |
| `SDL_ClearComposition` | yes | yes | yes | yes |
| `SDL_ClearError` | yes | yes | yes | yes |
| `SDL_ClearHints` | yes | yes | yes | yes |
| `SDL_ClearQueuedAudio` | yes | yes | yes | yes |
| `SDL_CloseAudio` | yes | yes | yes | yes |
| `SDL_CloseAudioDevice` | yes | yes | yes | yes |
| `SDL_ComposeCustomBlendMode` | yes | yes | yes | yes |
| `SDL_CondBroadcast` | yes | yes | no | yes |
| `SDL_CondSignal` | yes | yes | no | yes |
| `SDL_CondWait` | yes | yes | no | yes |
| `SDL_CondWaitTimeout` | yes | yes | no | yes |
| `SDL_ConvertAudio` | yes | yes | no | yes |
| `SDL_ConvertPixels` | yes | yes | yes | yes |
| `SDL_ConvertSurface` | yes | yes | yes | yes |
| `SDL_ConvertSurfaceFormat` | yes | yes | yes | yes |
| ... | 823 additional sorted function(s) omitted for readability. |  |  |  |

### Evidence Gaps

No evidence gaps were detected for loaded sources and manifest-required checks.


## Family: sdl2-image

- Display name: SDL2 Image
- Expected namespace: `SDL2.Image`
- Expected raw class: `SDL_imageNative`

### Source Status

| Source | Path | Status | Functions | Constants | Types |
| --- | --- | --- | ---: | ---: | ---: |
| Cake Preview | `n/a` | n/a | 0 | 0 | 0 |
| ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Compat` | present | 59 | 4 | 3 |
| ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern` | present | 59 | 4 | 3 |
| SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| SDL2-CS | `external/sdl2-cs/src/SDL2_image.cs` | present | 29 | 4 | 3 |

### Surface Counts

| Surface | Functions | Constants | Types |
| --- | ---: | ---: | ---: |
| Generated ClangSharp | 118 | 8 | 6 |
| Cake Preview | 0 | 0 | 0 |
| SDL2-CS | 29 | 4 | 3 |
| Dynapi | 0 | 0 | 0 |
| Manifest Required Surface | 0 | 0 | 0 |

### Raw ABI Constitution Checks

No raw ABI constitution checks were produced.

### Function Matrix

| Function | Generated | Cake | SDL2-CS | Dynapi |
| --- | --- | --- | --- | --- |
| `IMG_FreeAnimation` | yes | n/a | yes | n/a |
| `IMG_Init` | yes | n/a | yes | n/a |
| `IMG_Linked_Version` | yes | n/a | yes | n/a |
| `IMG_Load` | yes | n/a | yes | n/a |
| `IMG_LoadAVIF_RW` | yes | n/a | no | n/a |
| `IMG_LoadAnimation` | yes | n/a | yes | n/a |
| `IMG_LoadAnimationTyped_RW` | yes | n/a | yes | n/a |
| `IMG_LoadAnimation_RW` | yes | n/a | yes | n/a |
| `IMG_LoadBMP_RW` | yes | n/a | no | n/a |
| `IMG_LoadCUR_RW` | yes | n/a | no | n/a |
| `IMG_LoadGIFAnimation_RW` | yes | n/a | yes | n/a |
| `IMG_LoadGIF_RW` | yes | n/a | no | n/a |
| `IMG_LoadICO_RW` | yes | n/a | no | n/a |
| `IMG_LoadJPG_RW` | yes | n/a | no | n/a |
| `IMG_LoadJXL_RW` | yes | n/a | no | n/a |
| `IMG_LoadLBM_RW` | yes | n/a | no | n/a |
| `IMG_LoadPCX_RW` | yes | n/a | no | n/a |
| `IMG_LoadPNG_RW` | yes | n/a | no | n/a |
| `IMG_LoadPNM_RW` | yes | n/a | no | n/a |
| `IMG_LoadQOI_RW` | yes | n/a | no | n/a |
| `IMG_LoadSVG_RW` | yes | n/a | no | n/a |
| `IMG_LoadSizedSVG_RW` | yes | n/a | no | n/a |
| `IMG_LoadTGA_RW` | yes | n/a | no | n/a |
| `IMG_LoadTIF_RW` | yes | n/a | no | n/a |
| `IMG_LoadTexture` | yes | n/a | yes | n/a |
| `IMG_LoadTextureTyped_RW` | yes | n/a | yes | n/a |
| `IMG_LoadTexture_RW` | yes | n/a | yes | n/a |
| `IMG_LoadTyped_RW` | yes | n/a | yes | n/a |
| `IMG_LoadWEBPAnimation_RW` | yes | n/a | no | n/a |
| `IMG_LoadWEBP_RW` | yes | n/a | no | n/a |
| `IMG_LoadXCF_RW` | yes | n/a | no | n/a |
| `IMG_LoadXPM_RW` | yes | n/a | no | n/a |
| `IMG_LoadXV_RW` | yes | n/a | no | n/a |
| `IMG_Load_RW` | yes | n/a | yes | n/a |
| `IMG_Quit` | yes | n/a | yes | n/a |
| `IMG_ReadXPMFromArray` | yes | n/a | yes | n/a |
| `IMG_ReadXPMFromArrayToRGB888` | yes | n/a | no | n/a |
| `IMG_SaveJPG` | yes | n/a | yes | n/a |
| `IMG_SaveJPG_RW` | yes | n/a | yes | n/a |
| `IMG_SavePNG` | yes | n/a | yes | n/a |
| `IMG_SavePNG_RW` | yes | n/a | yes | n/a |
| `IMG_isAVIF` | yes | n/a | no | n/a |
| `IMG_isBMP` | yes | n/a | no | n/a |
| `IMG_isCUR` | yes | n/a | no | n/a |
| `IMG_isGIF` | yes | n/a | no | n/a |
| `IMG_isICO` | yes | n/a | no | n/a |
| `IMG_isJPG` | yes | n/a | no | n/a |
| `IMG_isJXL` | yes | n/a | no | n/a |
| `IMG_isLBM` | yes | n/a | no | n/a |
| `IMG_isPCX` | yes | n/a | no | n/a |
| ... | 9 additional sorted function(s) omitted for readability. |  |  |  |

### Evidence Gaps

No evidence gaps were detected for loaded sources and manifest-required checks.

