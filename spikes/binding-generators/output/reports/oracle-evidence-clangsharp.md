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
| sdl2-gfx | ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Compat` | present | 102 | 8 | 5 |
| sdl2-gfx | ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Modern` | present | 102 | 8 | 5 |
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
| sdl2-mixer | ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat` | present | 101 | 17 | 6 |
| sdl2-mixer | ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern` | present | 101 | 17 | 6 |
| sdl2-mixer | SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| sdl2-mixer | Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| sdl2-mixer | SDL2-CS | `external/sdl2-cs/src/SDL2_mixer.cs` | present | 104 | 9 | 5 |
| sdl2-ttf | Cake Preview | `n/a` | n/a | 0 | 0 | 0 |
| sdl2-ttf | ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat` | present | 99 | 25 | 3 |
| sdl2-ttf | ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern` | present | 89 | 25 | 3 |
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
| ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Compat` | present | 102 | 8 | 5 |
| ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Modern` | present | 102 | 8 | 5 |
| SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| SDL2-CS | `external/sdl2-cs/src/SDL2_gfx.cs` | present | 102 | 10 | 2 |

### Surface Counts

| Surface | Functions | Constants | Types |
| --- | ---: | ---: | ---: |
| Generated ClangSharp | 204 | 16 | 10 |
| Cake Preview | 0 | 0 | 0 |
| SDL2-CS | 102 | 10 | 2 |
| Dynapi | 0 | 0 | 0 |
| Manifest Required Surface | 0 | 0 | 0 |

### Raw ABI Constitution Checks

No raw ABI constitution checks were produced.

### Function Matrix

| Function | Generated | Cake | SDL2-CS | Dynapi |
| --- | --- | --- | --- | --- |
| `SDL_framerateDelay` | yes | n/a | yes | n/a |
| `SDL_getFramecount` | yes | n/a | yes | n/a |
| `SDL_getFramerate` | yes | n/a | yes | n/a |
| `SDL_imageFilterAbsDiff` | yes | n/a | yes | n/a |
| `SDL_imageFilterAdd` | yes | n/a | yes | n/a |
| `SDL_imageFilterAddByte` | yes | n/a | yes | n/a |
| `SDL_imageFilterAddByteToHalf` | yes | n/a | yes | n/a |
| `SDL_imageFilterAddUint` | yes | n/a | yes | n/a |
| `SDL_imageFilterBinarizeUsingThreshold` | yes | n/a | yes | n/a |
| `SDL_imageFilterBitAnd` | yes | n/a | yes | n/a |
| `SDL_imageFilterBitNegation` | yes | n/a | yes | n/a |
| `SDL_imageFilterBitOr` | yes | n/a | yes | n/a |
| `SDL_imageFilterClipToRange` | yes | n/a | yes | n/a |
| `SDL_imageFilterDiv` | yes | n/a | yes | n/a |
| `SDL_imageFilterMMXdetect` | yes | n/a | yes | n/a |
| `SDL_imageFilterMMXoff` | yes | n/a | yes | n/a |
| `SDL_imageFilterMMXon` | yes | n/a | yes | n/a |
| `SDL_imageFilterMean` | yes | n/a | yes | n/a |
| `SDL_imageFilterMult` | yes | n/a | yes | n/a |
| `SDL_imageFilterMultByByte` | yes | n/a | yes | n/a |
| `SDL_imageFilterMultDivby2` | yes | n/a | yes | n/a |
| `SDL_imageFilterMultDivby4` | yes | n/a | yes | n/a |
| `SDL_imageFilterMultNor` | yes | n/a | yes | n/a |
| `SDL_imageFilterNormalizeLinear` | yes | n/a | yes | n/a |
| `SDL_imageFilterShiftLeft` | yes | n/a | yes | n/a |
| `SDL_imageFilterShiftLeftByte` | yes | n/a | yes | n/a |
| `SDL_imageFilterShiftLeftUint` | yes | n/a | yes | n/a |
| `SDL_imageFilterShiftRight` | yes | n/a | yes | n/a |
| `SDL_imageFilterShiftRightAndMultByByte` | yes | n/a | yes | n/a |
| `SDL_imageFilterShiftRightUint` | yes | n/a | yes | n/a |
| `SDL_imageFilterSub` | yes | n/a | yes | n/a |
| `SDL_imageFilterSubByte` | yes | n/a | yes | n/a |
| `SDL_imageFilterSubUint` | yes | n/a | yes | n/a |
| `SDL_initFramerate` | yes | n/a | yes | n/a |
| `SDL_setFramerate` | yes | n/a | yes | n/a |
| `aacircleColor` | yes | n/a | yes | n/a |
| `aacircleRGBA` | yes | n/a | yes | n/a |
| `aaellipseColor` | yes | n/a | yes | n/a |
| `aaellipseRGBA` | yes | n/a | yes | n/a |
| `aalineColor` | yes | n/a | yes | n/a |
| `aalineRGBA` | yes | n/a | yes | n/a |
| `aapolygonColor` | yes | n/a | yes | n/a |
| `aapolygonRGBA` | yes | n/a | yes | n/a |
| `aatrigonColor` | yes | n/a | yes | n/a |
| `aatrigonRGBA` | yes | n/a | yes | n/a |
| `arcColor` | yes | n/a | yes | n/a |
| `arcRGBA` | yes | n/a | yes | n/a |
| `bezierColor` | yes | n/a | yes | n/a |
| `bezierRGBA` | yes | n/a | yes | n/a |
| `boxColor` | yes | n/a | yes | n/a |
| ... | 52 additional sorted function(s) omitted for readability. |  |  |  |

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


## Family: sdl2-mixer

- Display name: SDL2 Mixer
- Expected namespace: `SDL2.Mixer`
- Expected raw class: `SDL_mixerNative`

### Source Status

| Source | Path | Status | Functions | Constants | Types |
| --- | --- | --- | ---: | ---: | ---: |
| Cake Preview | `n/a` | n/a | 0 | 0 | 0 |
| ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat` | present | 101 | 17 | 6 |
| ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern` | present | 101 | 17 | 6 |
| SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| SDL2-CS | `external/sdl2-cs/src/SDL2_mixer.cs` | present | 104 | 9 | 5 |

### Surface Counts

| Surface | Functions | Constants | Types |
| --- | ---: | ---: | ---: |
| Generated ClangSharp | 202 | 34 | 12 |
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
| `Mix_AllocateChannels` | yes | n/a | yes | n/a |
| `Mix_ChannelFinished` | yes | n/a | yes | n/a |
| `Mix_CloseAudio` | yes | n/a | yes | n/a |
| `Mix_EachSoundFont` | yes | n/a | yes | n/a |
| `Mix_ExpireChannel` | yes | n/a | yes | n/a |
| `Mix_FadeInChannel` | yes | n/a | no | n/a |
| `Mix_FadeInChannelTimed` | yes | n/a | yes | n/a |
| `Mix_FadeInMusic` | yes | n/a | yes | n/a |
| `Mix_FadeInMusicPos` | yes | n/a | yes | n/a |
| `Mix_FadeOutChannel` | yes | n/a | yes | n/a |
| `Mix_FadeOutGroup` | yes | n/a | yes | n/a |
| `Mix_FadeOutMusic` | yes | n/a | yes | n/a |
| `Mix_FadingChannel` | yes | n/a | yes | n/a |
| `Mix_FadingMusic` | yes | n/a | yes | n/a |
| `Mix_FreeChunk` | yes | n/a | yes | n/a |
| `Mix_FreeMusic` | yes | n/a | yes | n/a |
| `Mix_GetChunk` | yes | n/a | yes | n/a |
| `Mix_GetChunkDecoder` | yes | n/a | yes | n/a |
| `Mix_GetMusicAlbumTag` | yes | n/a | yes | n/a |
| `Mix_GetMusicArtistTag` | yes | n/a | yes | n/a |
| `Mix_GetMusicCopyrightTag` | yes | n/a | yes | n/a |
| `Mix_GetMusicDecoder` | yes | n/a | yes | n/a |
| `Mix_GetMusicHookData` | yes | n/a | yes | n/a |
| `Mix_GetMusicLoopEndTime` | yes | n/a | yes | n/a |
| `Mix_GetMusicLoopLengthTime` | yes | n/a | yes | n/a |
| `Mix_GetMusicLoopStartTime` | yes | n/a | yes | n/a |
| `Mix_GetMusicPosition` | yes | n/a | yes | n/a |
| `Mix_GetMusicTitle` | yes | n/a | yes | n/a |
| `Mix_GetMusicTitleTag` | yes | n/a | yes | n/a |
| `Mix_GetMusicType` | yes | n/a | yes | n/a |
| `Mix_GetMusicVolume` | yes | n/a | no | n/a |
| `Mix_GetNumChunkDecoders` | yes | n/a | yes | n/a |
| `Mix_GetNumMusicDecoders` | yes | n/a | yes | n/a |
| `Mix_GetNumTracks` | yes | n/a | no | n/a |
| `Mix_GetSoundFonts` | yes | n/a | yes | n/a |
| `Mix_GetSynchroValue` | yes | n/a | yes | n/a |
| `Mix_GetTimidityCfg` | yes | n/a | yes | n/a |
| `Mix_GetVolumeMusicStream` | no | n/a | yes | n/a |
| `Mix_GroupAvailable` | yes | n/a | yes | n/a |
| `Mix_GroupChannel` | yes | n/a | yes | n/a |
| `Mix_GroupChannels` | yes | n/a | yes | n/a |
| `Mix_GroupCount` | yes | n/a | yes | n/a |
| `Mix_GroupNewer` | yes | n/a | yes | n/a |
| `Mix_GroupOldest` | yes | n/a | yes | n/a |
| `Mix_HaltChannel` | yes | n/a | yes | n/a |
| `Mix_HaltGroup` | yes | n/a | yes | n/a |
| `Mix_HaltMusic` | yes | n/a | yes | n/a |
| `Mix_HasChunkDecoder` | yes | n/a | no | n/a |
| `Mix_HasMusicDecoder` | yes | n/a | no | n/a |
| ... | 49 additional sorted function(s) omitted for readability. |  |  |  |

### Evidence Gaps

No evidence gaps were detected for loaded sources and manifest-required checks.


## Family: sdl2-ttf

- Display name: SDL2 TTF
- Expected namespace: `SDL2.Ttf`
- Expected raw class: `SDL_ttfNative`

### Source Status

| Source | Path | Status | Functions | Constants | Types |
| --- | --- | --- | ---: | ---: | ---: |
| Cake Preview | `n/a` | n/a | 0 | 0 | 0 |
| ClangSharp Compat | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat` | present | 99 | 25 | 3 |
| ClangSharp Modern | `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern` | present | 89 | 25 | 3 |
| SDL2 Dynapi | `external/vcpkg or vcpkg_installed` | n/a | 0 | 0 | 0 |
| Manifest Required Surface | `build/manifest.json` | n/a | 0 | 0 | 0 |
| SDL2-CS | `external/sdl2-cs/src/SDL2_ttf.cs` | present | 82 | 16 | 1 |

### Surface Counts

| Surface | Functions | Constants | Types |
| --- | ---: | ---: | ---: |
| Generated ClangSharp | 188 | 50 | 6 |
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
| `TTF_ByteSwappedUNICODE` | yes | n/a | yes | n/a |
| `TTF_CloseFont` | yes | n/a | yes | n/a |
| `TTF_FontAscent` | yes | n/a | yes | n/a |
| `TTF_FontDescent` | yes | n/a | yes | n/a |
| `TTF_FontFaceFamilyName` | yes | n/a | yes | n/a |
| `TTF_FontFaceIsFixedWidth` | yes | n/a | yes | n/a |
| `TTF_FontFaceStyleName` | yes | n/a | yes | n/a |
| `TTF_FontFaces` | yes | n/a | yes | n/a |
| `TTF_FontHeight` | yes | n/a | yes | n/a |
| `TTF_FontLineSkip` | yes | n/a | yes | n/a |
| `TTF_GetFontHinting` | yes | n/a | yes | n/a |
| `TTF_GetFontKerning` | yes | n/a | yes | n/a |
| `TTF_GetFontKerningSizeGlyphs` | yes | n/a | yes | n/a |
| `TTF_GetFontKerningSizeGlyphs32` | yes | n/a | yes | n/a |
| `TTF_GetFontOutline` | yes | n/a | yes | n/a |
| `TTF_GetFontSDF` | yes | n/a | no | n/a |
| `TTF_GetFontStyle` | yes | n/a | yes | n/a |
| `TTF_GetFontWrappedAlign` | yes | n/a | no | n/a |
| `TTF_GetFreeTypeVersion` | yes | n/a | no | n/a |
| `TTF_GetHarfBuzzVersion` | yes | n/a | no | n/a |
| `TTF_GlyphIsProvided` | yes | n/a | yes | n/a |
| `TTF_GlyphIsProvided32` | yes | n/a | yes | n/a |
| `TTF_GlyphMetrics` | yes | n/a | yes | n/a |
| `TTF_GlyphMetrics32` | yes | n/a | yes | n/a |
| `TTF_Init` | yes | n/a | yes | n/a |
| `TTF_LinkedVersion` | no | n/a | yes | n/a |
| `TTF_Linked_Version` | yes | n/a | no | n/a |
| `TTF_MeasureText` | yes | n/a | yes | n/a |
| `TTF_MeasureUNICODE` | yes | n/a | yes | n/a |
| `TTF_MeasureUTF8` | yes | n/a | yes | n/a |
| `TTF_OpenFont` | yes | n/a | yes | n/a |
| `TTF_OpenFontDPI` | yes | n/a | no | n/a |
| `TTF_OpenFontDPIRW` | yes | n/a | no | n/a |
| `TTF_OpenFontIndex` | yes | n/a | yes | n/a |
| `TTF_OpenFontIndexDPI` | yes | n/a | no | n/a |
| `TTF_OpenFontIndexDPIRW` | yes | n/a | no | n/a |
| `TTF_OpenFontIndexRW` | yes | n/a | yes | n/a |
| `TTF_OpenFontRW` | yes | n/a | yes | n/a |
| `TTF_Quit` | yes | n/a | yes | n/a |
| `TTF_RenderGlyph32_Blended` | yes | n/a | yes | n/a |
| `TTF_RenderGlyph32_LCD` | yes | n/a | no | n/a |
| `TTF_RenderGlyph32_Shaded` | yes | n/a | yes | n/a |
| `TTF_RenderGlyph32_Solid` | yes | n/a | yes | n/a |
| `TTF_RenderGlyph_Blended` | yes | n/a | yes | n/a |
| `TTF_RenderGlyph_LCD` | yes | n/a | no | n/a |
| `TTF_RenderGlyph_Shaded` | yes | n/a | yes | n/a |
| `TTF_RenderGlyph_Solid` | yes | n/a | yes | n/a |
| `TTF_RenderText_Blended` | yes | n/a | yes | n/a |
| `TTF_RenderText_Blended_Wrapped` | yes | n/a | yes | n/a |
| ... | 39 additional sorted function(s) omitted for readability. |  |  |  |

### Evidence Gaps

No evidence gaps were detected for loaded sources and manifest-required checks.

