# ClangSharp Oracle Evidence

This spike report is evidence, not a correctness certificate. Raw ABI constitution checks are listed even when the generated code builds cleanly.

## Inputs

| Family | Source | Path | Status | Functions | Constants | Types |
| --- | --- | --- | --- | ---: | ---: | ---: |
| sdl2-core | Cake Preview | `artifacts/generated-bindings-preview/sdl2-core` | present | 854 | 351 | 152 |
| sdl2-core | ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat` | present | 933 | 413 | 204 |
| sdl2-core | ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern` | present | 929 | 413 | 218 |
| sdl2-core | SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | present | 872 | 0 | 0 |
| sdl2-core | Manifest Required Surface | `build/manifest.json` | present | 5 | 10 | 0 |
| sdl2-core | SDL2-CS | `external/sdl2-cs/src/SDL2.cs` | present | 789 | 292 | 131 |
| sdl2-gfx | Cake Preview | `n/a` | n/a | 0 | 0 | 0 |
| sdl2-gfx | ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Compat` | missing | 0 | 0 | 0 |
| sdl2-gfx | ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Modern` | missing | 0 | 0 | 0 |
| sdl2-gfx | SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| sdl2-gfx | Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| sdl2-gfx | SDL2-CS | `external/sdl2-cs/src/SDL2_gfx.cs` | present | 102 | 10 | 2 |
| sdl2-image | Cake Preview | `n/a` | n/a | 0 | 0 | 0 |
| sdl2-image | ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Compat` | present | 59 | 4 | 3 |
| sdl2-image | ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern` | present | 59 | 4 | 3 |
| sdl2-image | SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| sdl2-image | Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| sdl2-image | SDL2-CS | `external/sdl2-cs/src/SDL2_image.cs` | present | 29 | 4 | 3 |
| sdl2-mixer | Cake Preview | `n/a` | n/a | 0 | 0 | 0 |
| sdl2-mixer | ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat` | missing | 0 | 0 | 0 |
| sdl2-mixer | ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern` | missing | 0 | 0 | 0 |
| sdl2-mixer | SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| sdl2-mixer | Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| sdl2-mixer | SDL2-CS | `external/sdl2-cs/src/SDL2_mixer.cs` | present | 104 | 9 | 5 |
| sdl2-ttf | Cake Preview | `n/a` | n/a | 0 | 0 | 0 |
| sdl2-ttf | ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat` | missing | 0 | 0 | 0 |
| sdl2-ttf | ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern` | missing | 0 | 0 | 0 |
| sdl2-ttf | SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| sdl2-ttf | Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| sdl2-ttf | SDL2-CS | `external/sdl2-cs/src/SDL2_ttf.cs` | present | 82 | 16 | 1 |

## Family: sdl2-core

- Display name: SDL2 Core
- Expected namespace: `SDL2`
- Expected raw class: `SDLNative`

### Source Status

| Source | Path | Status | Functions | Constants | Types |
| --- | --- | --- | ---: | ---: | ---: |
| Cake Preview | `artifacts/generated-bindings-preview/sdl2-core` | present | 854 | 351 | 152 |
| ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat` | present | 933 | 413 | 204 |
| ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern` | present | 929 | 413 | 218 |
| SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | present | 872 | 0 | 0 |
| Manifest Required Surface | `build/manifest.json` | present | 5 | 10 | 0 |
| SDL2-CS | `external/sdl2-cs/src/SDL2.cs` | present | 789 | 292 | 131 |

### Surface Counts

| Surface | Functions | Constants | Types |
| --- | ---: | ---: | ---: |
| Generated ClangSharp | 1862 | 826 | 422 |
| Cake Preview | 854 | 351 | 152 |
| SDL2-CS | 789 | 292 | 131 |
| Dynapi | 872 | 0 | 0 |
| Manifest Required Surface | 5 | 10 | 0 |

### Raw ABI Constitution Checks

No raw ABI constitution checks were produced.

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


## Family: sdl2-gfx

- Display name: SDL2 GFX
- Expected namespace: `SDL2.Gfx`
- Expected raw class: `SDL2_gfxNative`

### Source Status

| Source | Path | Status | Functions | Constants | Types |
| --- | --- | --- | ---: | ---: | ---: |
| Cake Preview | `n/a` | n/a | 0 | 0 | 0 |
| ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Compat` | missing | 0 | 0 | 0 |
| ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Modern` | missing | 0 | 0 | 0 |
| SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| SDL2-CS | `external/sdl2-cs/src/SDL2_gfx.cs` | present | 102 | 10 | 2 |

### Surface Counts

| Surface | Functions | Constants | Types |
| --- | ---: | ---: | ---: |
| Generated ClangSharp | 0 | 0 | 0 |
| Cake Preview | 0 | 0 | 0 |
| SDL2-CS | 102 | 10 | 2 |
| Dynapi | 0 | 0 | 0 |
| Manifest Required Surface | 0 | 0 | 0 |

### Raw ABI Constitution Checks

No raw ABI constitution checks were produced.

### Function Matrix

| Function | Generated | Cake | SDL2-CS | Dynapi |
| --- | --- | --- | --- | --- |
| `SDL_framerateDelay` | no | n/a | yes | n/a |
| `SDL_getFramecount` | no | n/a | yes | n/a |
| `SDL_getFramerate` | no | n/a | yes | n/a |
| `SDL_imageFilterAbsDiff` | no | n/a | yes | n/a |
| `SDL_imageFilterAdd` | no | n/a | yes | n/a |
| `SDL_imageFilterAddByte` | no | n/a | yes | n/a |
| `SDL_imageFilterAddByteToHalf` | no | n/a | yes | n/a |
| `SDL_imageFilterAddUint` | no | n/a | yes | n/a |
| `SDL_imageFilterBinarizeUsingThreshold` | no | n/a | yes | n/a |
| `SDL_imageFilterBitAnd` | no | n/a | yes | n/a |
| `SDL_imageFilterBitNegation` | no | n/a | yes | n/a |
| `SDL_imageFilterBitOr` | no | n/a | yes | n/a |
| `SDL_imageFilterClipToRange` | no | n/a | yes | n/a |
| `SDL_imageFilterDiv` | no | n/a | yes | n/a |
| `SDL_imageFilterMMXdetect` | no | n/a | yes | n/a |
| `SDL_imageFilterMMXoff` | no | n/a | yes | n/a |
| `SDL_imageFilterMMXon` | no | n/a | yes | n/a |
| `SDL_imageFilterMean` | no | n/a | yes | n/a |
| `SDL_imageFilterMult` | no | n/a | yes | n/a |
| `SDL_imageFilterMultByByte` | no | n/a | yes | n/a |
| `SDL_imageFilterMultDivby2` | no | n/a | yes | n/a |
| `SDL_imageFilterMultDivby4` | no | n/a | yes | n/a |
| `SDL_imageFilterMultNor` | no | n/a | yes | n/a |
| `SDL_imageFilterNormalizeLinear` | no | n/a | yes | n/a |
| `SDL_imageFilterShiftLeft` | no | n/a | yes | n/a |
| `SDL_imageFilterShiftLeftByte` | no | n/a | yes | n/a |
| `SDL_imageFilterShiftLeftUint` | no | n/a | yes | n/a |
| `SDL_imageFilterShiftRight` | no | n/a | yes | n/a |
| `SDL_imageFilterShiftRightAndMultByByte` | no | n/a | yes | n/a |
| `SDL_imageFilterShiftRightUint` | no | n/a | yes | n/a |
| `SDL_imageFilterSub` | no | n/a | yes | n/a |
| `SDL_imageFilterSubByte` | no | n/a | yes | n/a |
| `SDL_imageFilterSubUint` | no | n/a | yes | n/a |
| `SDL_initFramerate` | no | n/a | yes | n/a |
| `SDL_setFramerate` | no | n/a | yes | n/a |
| `aacircleColor` | no | n/a | yes | n/a |
| `aacircleRGBA` | no | n/a | yes | n/a |
| `aaellipseColor` | no | n/a | yes | n/a |
| `aaellipseRGBA` | no | n/a | yes | n/a |
| `aalineColor` | no | n/a | yes | n/a |
| `aalineRGBA` | no | n/a | yes | n/a |
| `aapolygonColor` | no | n/a | yes | n/a |
| `aapolygonRGBA` | no | n/a | yes | n/a |
| `aatrigonColor` | no | n/a | yes | n/a |
| `aatrigonRGBA` | no | n/a | yes | n/a |
| `arcColor` | no | n/a | yes | n/a |
| `arcRGBA` | no | n/a | yes | n/a |
| `bezierColor` | no | n/a | yes | n/a |
| `bezierRGBA` | no | n/a | yes | n/a |
| `boxColor` | no | n/a | yes | n/a |
| ... | 52 additional sorted function(s) omitted for readability. |  |  |  |

### Evidence Gaps

- Evidence Missing: ClangSharp Compat at `spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Compat`.
- Evidence Missing: ClangSharp Modern at `spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Modern`.


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


## Family: sdl2-mixer

- Display name: SDL2 Mixer
- Expected namespace: `SDL2.Mixer`
- Expected raw class: `SDL_mixerNative`

### Source Status

| Source | Path | Status | Functions | Constants | Types |
| --- | --- | --- | ---: | ---: | ---: |
| Cake Preview | `n/a` | n/a | 0 | 0 | 0 |
| ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat` | missing | 0 | 0 | 0 |
| ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern` | missing | 0 | 0 | 0 |
| SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| SDL2-CS | `external/sdl2-cs/src/SDL2_mixer.cs` | present | 104 | 9 | 5 |

### Surface Counts

| Surface | Functions | Constants | Types |
| --- | ---: | ---: | ---: |
| Generated ClangSharp | 0 | 0 | 0 |
| Cake Preview | 0 | 0 | 0 |
| SDL2-CS | 104 | 9 | 5 |
| Dynapi | 0 | 0 | 0 |
| Manifest Required Surface | 0 | 0 | 0 |

### Raw ABI Constitution Checks

No raw ABI constitution checks were produced.

### Function Matrix

| Function | Generated | Cake | SDL2-CS | Dynapi |
| --- | --- | --- | --- | --- |
| `MIX_Linked_Version` | no | n/a | yes | n/a |
| `Mix_AllocateChannels` | no | n/a | yes | n/a |
| `Mix_ChannelFinished` | no | n/a | yes | n/a |
| `Mix_CloseAudio` | no | n/a | yes | n/a |
| `Mix_EachSoundFont` | no | n/a | yes | n/a |
| `Mix_ExpireChannel` | no | n/a | yes | n/a |
| `Mix_FadeInChannelTimed` | no | n/a | yes | n/a |
| `Mix_FadeInMusic` | no | n/a | yes | n/a |
| `Mix_FadeInMusicPos` | no | n/a | yes | n/a |
| `Mix_FadeOutChannel` | no | n/a | yes | n/a |
| `Mix_FadeOutGroup` | no | n/a | yes | n/a |
| `Mix_FadeOutMusic` | no | n/a | yes | n/a |
| `Mix_FadingChannel` | no | n/a | yes | n/a |
| `Mix_FadingMusic` | no | n/a | yes | n/a |
| `Mix_FreeChunk` | no | n/a | yes | n/a |
| `Mix_FreeMusic` | no | n/a | yes | n/a |
| `Mix_GetChunk` | no | n/a | yes | n/a |
| `Mix_GetChunkDecoder` | no | n/a | yes | n/a |
| `Mix_GetMusicAlbumTag` | no | n/a | yes | n/a |
| `Mix_GetMusicArtistTag` | no | n/a | yes | n/a |
| `Mix_GetMusicCopyrightTag` | no | n/a | yes | n/a |
| `Mix_GetMusicDecoder` | no | n/a | yes | n/a |
| `Mix_GetMusicHookData` | no | n/a | yes | n/a |
| `Mix_GetMusicLoopEndTime` | no | n/a | yes | n/a |
| `Mix_GetMusicLoopLengthTime` | no | n/a | yes | n/a |
| `Mix_GetMusicLoopStartTime` | no | n/a | yes | n/a |
| `Mix_GetMusicPosition` | no | n/a | yes | n/a |
| `Mix_GetMusicTitle` | no | n/a | yes | n/a |
| `Mix_GetMusicTitleTag` | no | n/a | yes | n/a |
| `Mix_GetMusicType` | no | n/a | yes | n/a |
| `Mix_GetNumChunkDecoders` | no | n/a | yes | n/a |
| `Mix_GetNumMusicDecoders` | no | n/a | yes | n/a |
| `Mix_GetSoundFonts` | no | n/a | yes | n/a |
| `Mix_GetSynchroValue` | no | n/a | yes | n/a |
| `Mix_GetTimidityCfg` | no | n/a | yes | n/a |
| `Mix_GetVolumeMusicStream` | no | n/a | yes | n/a |
| `Mix_GroupAvailable` | no | n/a | yes | n/a |
| `Mix_GroupChannel` | no | n/a | yes | n/a |
| `Mix_GroupChannels` | no | n/a | yes | n/a |
| `Mix_GroupCount` | no | n/a | yes | n/a |
| `Mix_GroupNewer` | no | n/a | yes | n/a |
| `Mix_GroupOldest` | no | n/a | yes | n/a |
| `Mix_HaltChannel` | no | n/a | yes | n/a |
| `Mix_HaltGroup` | no | n/a | yes | n/a |
| `Mix_HaltMusic` | no | n/a | yes | n/a |
| `Mix_HookMusic` | no | n/a | yes | n/a |
| `Mix_HookMusicFinished` | no | n/a | yes | n/a |
| `Mix_Init` | no | n/a | yes | n/a |
| `Mix_LoadMUS` | no | n/a | yes | n/a |
| `Mix_LoadWAV_RW` | no | n/a | yes | n/a |
| ... | 34 additional sorted function(s) omitted for readability. |  |  |  |

### Evidence Gaps

- Evidence Missing: ClangSharp Compat at `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat`.
- Evidence Missing: ClangSharp Modern at `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern`.


## Family: sdl2-ttf

- Display name: SDL2 TTF
- Expected namespace: `SDL2.Ttf`
- Expected raw class: `SDL_ttfNative`

### Source Status

| Source | Path | Status | Functions | Constants | Types |
| --- | --- | --- | ---: | ---: | ---: |
| Cake Preview | `n/a` | n/a | 0 | 0 | 0 |
| ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat` | missing | 0 | 0 | 0 |
| ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern` | missing | 0 | 0 | 0 |
| SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| SDL2-CS | `external/sdl2-cs/src/SDL2_ttf.cs` | present | 82 | 16 | 1 |

### Surface Counts

| Surface | Functions | Constants | Types |
| --- | ---: | ---: | ---: |
| Generated ClangSharp | 0 | 0 | 0 |
| Cake Preview | 0 | 0 | 0 |
| SDL2-CS | 82 | 16 | 1 |
| Dynapi | 0 | 0 | 0 |
| Manifest Required Surface | 0 | 0 | 0 |

### Raw ABI Constitution Checks

No raw ABI constitution checks were produced.

### Function Matrix

| Function | Generated | Cake | SDL2-CS | Dynapi |
| --- | --- | --- | --- | --- |
| `SDL_GetFontKerningSize` | no | n/a | yes | n/a |
| `TTF_ByteSwappedUNICODE` | no | n/a | yes | n/a |
| `TTF_CloseFont` | no | n/a | yes | n/a |
| `TTF_FontAscent` | no | n/a | yes | n/a |
| `TTF_FontDescent` | no | n/a | yes | n/a |
| `TTF_FontFaceFamilyName` | no | n/a | yes | n/a |
| `TTF_FontFaceIsFixedWidth` | no | n/a | yes | n/a |
| `TTF_FontFaceStyleName` | no | n/a | yes | n/a |
| `TTF_FontFaces` | no | n/a | yes | n/a |
| `TTF_FontHeight` | no | n/a | yes | n/a |
| `TTF_FontLineSkip` | no | n/a | yes | n/a |
| `TTF_GetFontHinting` | no | n/a | yes | n/a |
| `TTF_GetFontKerning` | no | n/a | yes | n/a |
| `TTF_GetFontKerningSizeGlyphs` | no | n/a | yes | n/a |
| `TTF_GetFontKerningSizeGlyphs32` | no | n/a | yes | n/a |
| `TTF_GetFontOutline` | no | n/a | yes | n/a |
| `TTF_GetFontStyle` | no | n/a | yes | n/a |
| `TTF_GlyphIsProvided` | no | n/a | yes | n/a |
| `TTF_GlyphIsProvided32` | no | n/a | yes | n/a |
| `TTF_GlyphMetrics` | no | n/a | yes | n/a |
| `TTF_GlyphMetrics32` | no | n/a | yes | n/a |
| `TTF_Init` | no | n/a | yes | n/a |
| `TTF_LinkedVersion` | no | n/a | yes | n/a |
| `TTF_MeasureText` | no | n/a | yes | n/a |
| `TTF_MeasureUNICODE` | no | n/a | yes | n/a |
| `TTF_MeasureUTF8` | no | n/a | yes | n/a |
| `TTF_OpenFont` | no | n/a | yes | n/a |
| `TTF_OpenFontIndex` | no | n/a | yes | n/a |
| `TTF_OpenFontIndexRW` | no | n/a | yes | n/a |
| `TTF_OpenFontRW` | no | n/a | yes | n/a |
| `TTF_Quit` | no | n/a | yes | n/a |
| `TTF_RenderGlyph32_Blended` | no | n/a | yes | n/a |
| `TTF_RenderGlyph32_Shaded` | no | n/a | yes | n/a |
| `TTF_RenderGlyph32_Solid` | no | n/a | yes | n/a |
| `TTF_RenderGlyph_Blended` | no | n/a | yes | n/a |
| `TTF_RenderGlyph_Shaded` | no | n/a | yes | n/a |
| `TTF_RenderGlyph_Solid` | no | n/a | yes | n/a |
| `TTF_RenderText_Blended` | no | n/a | yes | n/a |
| `TTF_RenderText_Blended_Wrapped` | no | n/a | yes | n/a |
| `TTF_RenderText_Shaded` | no | n/a | yes | n/a |
| `TTF_RenderText_Shaded_Wrapped` | no | n/a | yes | n/a |
| `TTF_RenderText_Solid` | no | n/a | yes | n/a |
| `TTF_RenderText_Solid_Wrapped` | no | n/a | yes | n/a |
| `TTF_RenderUNICODE_Blended` | no | n/a | yes | n/a |
| `TTF_RenderUNICODE_Blended_Wrapped` | no | n/a | yes | n/a |
| `TTF_RenderUNICODE_Shaded` | no | n/a | yes | n/a |
| `TTF_RenderUNICODE_Shaded_Wrapped` | no | n/a | yes | n/a |
| `TTF_RenderUNICODE_Solid` | no | n/a | yes | n/a |
| `TTF_RenderUNICODE_Solid_Wrapped` | no | n/a | yes | n/a |
| `TTF_RenderUTF8_Blended` | no | n/a | yes | n/a |
| ... | 16 additional sorted function(s) omitted for readability. |  |  |  |

### Evidence Gaps

- Evidence Missing: ClangSharp Compat at `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat`.
- Evidence Missing: ClangSharp Modern at `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern`.

