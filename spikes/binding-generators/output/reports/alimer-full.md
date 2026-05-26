# Alimer-Style CppAst Full Report

**Triplet:** x64-windows-hybrid
**Mode:** emit

| Family | Headers | Functions Seen | Functions Emitted | Functions Skipped |
| --- | ---: | ---: | ---: | ---: |
| core | 51 | 854 | 795 | 59 |
| image | 1 | 59 | 51 | 8 |

## Skips

### UnsupportedValueType (50)

| Family | Header | Function | Detail |
| --- | --- | --- | --- |
| image | SDL_image.h | `IMG_FreeAnimation` | parameter[0] 'anim' unmapped |
| image | SDL_image.h | `IMG_LoadAnimation` | return type unmapped |
| image | SDL_image.h | `IMG_LoadAnimationTyped_RW` | return type unmapped |
| image | SDL_image.h | `IMG_LoadAnimation_RW` | return type unmapped |
| image | SDL_image.h | `IMG_LoadGIFAnimation_RW` | return type unmapped |
| image | SDL_image.h | `IMG_LoadWEBPAnimation_RW` | return type unmapped |
| image | SDL_image.h | `IMG_ReadXPMFromArray` | parameter[0] 'xpm' unmapped |
| image | SDL_image.h | `IMG_ReadXPMFromArrayToRGB888` | parameter[0] 'xpm' unmapped |
| core | SDL_atomic.h | `SDL_AtomicCASPtr` | parameter[0] 'a' unmapped |
| core | SDL_atomic.h | `SDL_AtomicGetPtr` | parameter[0] 'a' unmapped |
| core | SDL_atomic.h | `SDL_AtomicSetPtr` | parameter[0] 'a' unmapped |
| core | SDL_thread.h | `SDL_CreateThread` | manifest/spike excluded |
| core | SDL_thread.h | `SDL_CreateThreadWithStackSize` | manifest/spike excluded |
| core | SDL_render.h | `SDL_CreateWindowAndRenderer` | parameter[3] 'window' unmapped |
| core | SDL_guid.h | `SDL_GUIDFromString` | return type unmapped |
| core | SDL_guid.h | `SDL_GUIDToString` | parameter[0] 'guid' unmapped |
| core | SDL_gamecontroller.h | `SDL_GameControllerGetBindForAxis` | return type unmapped |
| core | SDL_gamecontroller.h | `SDL_GameControllerGetBindForButton` | return type unmapped |
| core | SDL_audio.h | `SDL_GetDefaultAudioInfo` | parameter[0] 'name' unmapped |
| core | SDL_events.h | `SDL_GetEventFilter` | parameter[1] 'userdata' unmapped |
| core | SDL_render.h | `SDL_GetRenderDrawBlendMode` | parameter[1] 'blendMode' unmapped |
| core | SDL_surface.h | `SDL_GetSurfaceBlendMode` | parameter[1] 'blendMode' unmapped |
| core | SDL_render.h | `SDL_GetTextureBlendMode` | parameter[1] 'blendMode' unmapped |
| core | SDL_render.h | `SDL_GetTextureScaleMode` | parameter[1] 'scaleMode' unmapped |
| core | SDL_syswm.h | `SDL_GetWindowWMInfo` | manifest/spike excluded |
| core | SDL_audio.h | `SDL_LoadWAV_RW` | parameter[3] 'audio_buf' unmapped |
| core | SDL_render.h | `SDL_LockTexture` | parameter[2] 'pixels' unmapped |
| core | SDL_render.h | `SDL_LockTextureToSurface` | parameter[2] 'surface' unmapped |
| core | SDL_log.h | `SDL_LogGetOutputFunction` | parameter[1] 'userdata' unmapped |
| core | SDL_log.h | `SDL_LogMessageV` | manifest/spike excluded |
| core | SDL_rwops.h | `SDL_RWFromFP` | manifest/spike excluded |
| core | SDL_system.h | `SDL_RenderGetD3D11Device` | return type unmapped |
| core | SDL_system.h | `SDL_RenderGetD3D12Device` | return type unmapped |
| core | SDL_system.h | `SDL_RenderGetD3D9Device` | return type unmapped |
| core | SDL_vulkan.h | `SDL_Vulkan_CreateSurface` | parameter[1] 'instance' unmapped |
| core | SDL_vulkan.h | `SDL_Vulkan_GetInstanceExtensions` | parameter[2] 'pNames' unmapped |
| core | SDL_stdinc.h | `SDL_asprintf` | parameter[0] 'strp' unmapped |
| core | SDL_stdinc.h | `SDL_iconv` | parameter[0] 'cd' unmapped |
| core | SDL_stdinc.h | `SDL_iconv_close` | parameter[0] 'cd' unmapped |
| core | SDL_stdinc.h | `SDL_iconv_open` | return type unmapped |
| ... | ... | ... | (10 more) |

### FunctionPointer (17)

| Family | Header | Function | Detail |
| --- | --- | --- | --- |
| core | SDL_events.h | `SDL_AddEventWatch` | parameter[0] 'filter' unmapped |
| core | SDL_hints.h | `SDL_AddHintCallback` | parameter[1] 'callback' unmapped |
| core | SDL_timer.h | `SDL_AddTimer` | parameter[1] 'callback' unmapped |
| core | SDL_events.h | `SDL_DelEventWatch` | parameter[0] 'filter' unmapped |
| core | SDL_hints.h | `SDL_DelHintCallback` | parameter[1] 'callback' unmapped |
| core | SDL_events.h | `SDL_FilterEvents` | parameter[0] 'filter' unmapped |
| core | SDL_assert.h | `SDL_GetAssertionHandler` | return type unmapped |
| core | SDL_assert.h | `SDL_GetDefaultAssertionHandler` | return type unmapped |
| core | SDL_log.h | `SDL_LogSetOutputFunction` | parameter[0] 'callback' unmapped |
| core | SDL_assert.h | `SDL_SetAssertionHandler` | parameter[0] 'handler' unmapped |
| core | SDL_events.h | `SDL_SetEventFilter` | parameter[0] 'filter' unmapped |
| core | SDL_stdinc.h | `SDL_SetMemoryFunctions` | parameter[0] 'malloc_func' unmapped |
| core | SDL_video.h | `SDL_SetWindowHitTest` | parameter[1] 'callback' unmapped |
| core | SDL_system.h | `SDL_SetWindowsMessageHook` | parameter[0] 'callback' unmapped |
| core | SDL_thread.h | `SDL_TLSSet` | parameter[2] 'destructor' unmapped |
| core | SDL_stdinc.h | `SDL_bsearch` | parameter[4] 'compare' unmapped |
| core | SDL_stdinc.h | `SDL_qsort` | parameter[3] 'compare' unmapped |

## Parse Diagnostics

No diagnostics recorded.
