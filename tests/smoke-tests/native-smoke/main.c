/*
 * Native Smoke Test — Hybrid Static Validation
 *
 * Validates that all SDL2 satellite libraries and their baked-in codecs
 * are functional at runtime. Runs in two modes:
 *
 *   Headless (default):  No display, no audio device. Uses SDL dummy drivers.
 *                        Validates codec initialization via IMG_Init/Mix_Init bitmasks.
 *                        Suitable for CI and automated validation.
 *
 *   Interactive:         Real display + audio. Opens a window, loads assets, plays audio.
 *                        Build with -DSMOKE_INTERACTIVE=ON or use the *-interactive preset.
 *                        Suitable for local debugging (F5 in IDE).
 *
 * Exit code 0 = all tests passed. Non-zero = failure count.
 */

#include <SDL.h>
#include <SDL2_gfxPrimitives.h>
#include <SDL_image.h>
#include <SDL_mixer.h>
#include <SDL_ttf.h>
#ifdef SMOKE_INCLUDE_NET
#include <SDL_net.h>
#endif

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

/* ------------------------------------------------------------------ */
/* Test result tracking                                                */
/* ------------------------------------------------------------------ */

static int g_pass = 0;
static int g_fail = 0;

static void test_ok(const char *name)
{
    printf("  [PASS] %s\n", name);
    g_pass++;
}

static void test_fail(const char *name, const char *reason)
{
    printf("  [FAIL] %s — %s\n", name, reason);
    g_fail++;
}

#define CHECK(name, cond) \
    do { if (cond) test_ok(name); else test_fail(name, SDL_GetError()); } while(0)

#define CHECK_REASON(name, cond, reason) \
    do { if (cond) test_ok(name); else test_fail(name, reason); } while(0)

#define CHECK_FLAG(name, result, flag) \
    do { if ((result) & (flag)) test_ok(name); else test_fail(name, SDL_GetError()); } while(0)

static int contains_ignore_case(const char *text, const char *needle)
{
    size_t index = 0;
    size_t needle_length = 0;

    if (text == NULL || needle == NULL)
    {
        return 0;
    }

    needle_length = strlen(needle);
    if (needle_length == 0)
    {
        return 1;
    }

    for (; text[index] != '\0'; index++)
    {
        size_t offset = 0;
        while (offset < needle_length && text[index + offset] != '\0')
        {
            char left = text[index + offset];
            char right = needle[offset];

            if (left >= 'A' && left <= 'Z')
            {
                left = (char)(left - 'A' + 'a');
            }

            if (right >= 'A' && right <= 'Z')
            {
                right = (char)(right - 'A' + 'a');
            }

            if (left != right)
            {
                break;
            }

            offset++;
        }

        if (offset == needle_length)
        {
            return 1;
        }
    }

    return 0;
}

static int has_music_decoder(const char *expected_fragment)
{
    int decoder_index = 0;
    int decoder_count = Mix_GetNumMusicDecoders();

    for (decoder_index = 0; decoder_index < decoder_count; decoder_index++)
    {
        const char *decoder_name = Mix_GetMusicDecoder(decoder_index);
        if (contains_ignore_case(decoder_name, expected_fragment))
        {
            return 1;
        }
    }

    return 0;
}

static SDL_Surface *load_png_fixture_surface(void)
{
    SDL_Surface *surface = NULL;
    char *base_path = SDL_GetBasePath();

    if (base_path != NULL)
    {
        char fixture_path[1024];
        SDL_snprintf(fixture_path, sizeof(fixture_path), "%sjanset2d-sdl-min.png", base_path);
        surface = IMG_Load(fixture_path);
        SDL_free(base_path);

        if (surface != NULL)
        {
            return surface;
        }
    }

    return IMG_Load("janset2d-sdl-min.png");
}

/* ------------------------------------------------------------------ */
/* SDL2 Core                                                           */
/* ------------------------------------------------------------------ */

static void test_sdl2_core(void)
{
    printf("\n=== SDL2 Core ===\n");

#ifndef SMOKE_INTERACTIVE
    SDL_SetHint(SDL_HINT_VIDEODRIVER, "dummy");
    SDL_SetHint(SDL_HINT_AUDIODRIVER, "dummy");
#endif

    int rc = SDL_Init(SDL_INIT_AUDIO | SDL_INIT_TIMER | SDL_INIT_VIDEO);
    CHECK("SDL_Init(AUDIO|TIMER|VIDEO)", rc == 0);

    SDL_version ver;
    SDL_GetVersion(&ver);
    printf("  SDL version: %d.%d.%d\n", ver.major, ver.minor, ver.patch);
}

/* ------------------------------------------------------------------ */
/* SDL2_image — codec bitmask validation                               */
/* ------------------------------------------------------------------ */

static void test_sdl2_image(void)
{
    printf("\n=== SDL2_image ===\n");

    int flags = IMG_INIT_PNG | IMG_INIT_JPG | IMG_INIT_WEBP | IMG_INIT_TIF | IMG_INIT_AVIF;
    int result = IMG_Init(flags);

    CHECK_FLAG("IMG_Init: PNG",  result, IMG_INIT_PNG);
    CHECK_FLAG("IMG_Init: JPEG", result, IMG_INIT_JPG);
    CHECK_FLAG("IMG_Init: WebP", result, IMG_INIT_WEBP);
    CHECK_FLAG("IMG_Init: TIFF", result, IMG_INIT_TIF);
    CHECK_FLAG("IMG_Init: AVIF", result, IMG_INIT_AVIF);

    const SDL_version *img_ver = IMG_Linked_Version();
    printf("  SDL_image version: %d.%d.%d\n", img_ver->major, img_ver->minor, img_ver->patch);

    {
        SDL_Surface *png_surface = load_png_fixture_surface();
        CHECK("IMG_Load PNG fixture", png_surface != NULL);
        if (png_surface != NULL)
        {
            SDL_FreeSurface(png_surface);
        }
    }
}

/* ------------------------------------------------------------------ */
/* SDL2_mixer — codec bitmask validation                               */
/* ------------------------------------------------------------------ */

static int dummy_audio_driver_present(void)
{
    int driver_count = SDL_GetNumAudioDrivers();
    int index = 0;
    for (index = 0; index < driver_count; index++)
    {
        const char *name = SDL_GetAudioDriver(index);
        if (name != NULL && SDL_strcasecmp(name, "dummy") == 0)
        {
            return 1;
        }
    }

    return 0;
}

static void test_sdl2_mixer(void)
{
    printf("\n=== SDL2_mixer ===\n");

    int flags = MIX_INIT_FLAC | MIX_INIT_MOD | MIX_INIT_MP3 | MIX_INIT_OGG | MIX_INIT_MID | MIX_INIT_OPUS | MIX_INIT_WAVPACK;
    int result = Mix_Init(flags);

    CHECK_FLAG("Mix_Init: FLAC",       result, MIX_INIT_FLAC);
    CHECK_FLAG("Mix_Init: OGG Vorbis", result, MIX_INIT_OGG);
    CHECK_FLAG("Mix_Init: Opus",       result, MIX_INIT_OPUS);
    CHECK_FLAG("Mix_Init: MP3",        result, MIX_INIT_MP3);
    CHECK_FLAG("Mix_Init: MOD",        result, MIX_INIT_MOD);
    CHECK_FLAG("Mix_Init: MIDI",       result, MIX_INIT_MID);
    CHECK_FLAG("Mix_Init: WavPack",    result, MIX_INIT_WAVPACK);

    /* Mix_OpenAudio in headless mode relies on SDL's dummy audio driver.
       Some minimal Linux containers build SDL2 without any audio backend, in which case
       dummy is not registered and Mix_OpenAudio fails with an opaque
       "No such audio device" error. Assert the driver is present explicitly so the
       failure mode is diagnosable when it happens on a new triplet. */
    CHECK_REASON("SDL audio dummy driver registered",
                 dummy_audio_driver_present(),
                 "SDL2 was built without the dummy audio backend; Mix_OpenAudio will fail on this RID.");

    {
        int audio_rc = Mix_OpenAudio(MIX_DEFAULT_FREQUENCY, MIX_DEFAULT_FORMAT, MIX_DEFAULT_CHANNELS, 1024);
        CHECK("Mix_OpenAudio", audio_rc == 0);
    }

    const SDL_version *mix_ver = Mix_Linked_Version();
    printf("  SDL_mixer version: %d.%d.%d\n", mix_ver->major, mix_ver->minor, mix_ver->patch);

    {
        int decoder_index = 0;
        int decoder_count = Mix_GetNumMusicDecoders();

        printf("  Music decoders (%d):", decoder_count);
        for (decoder_index = 0; decoder_index < decoder_count; decoder_index++)
        {
            const char *decoder_name = Mix_GetMusicDecoder(decoder_index);
            printf(" %s", decoder_name != NULL ? decoder_name : "<null>");
        }
        printf("\n");

        CHECK_REASON("Mix music decoders discovered", decoder_count > 0, "No SDL_mixer music decoders reported");
        CHECK_REASON("Mix decoder: OGG", has_music_decoder("OGG"), "OGG decoder missing");
        CHECK_REASON("Mix decoder: Opus", has_music_decoder("OPUS"), "Opus decoder missing");
        CHECK_REASON("Mix decoder: MP3", has_music_decoder("MP3"), "MP3 decoder missing");
        CHECK_REASON("Mix decoder: MOD", has_music_decoder("MOD"), "MOD decoder missing");
        CHECK_REASON("Mix decoder: FLAC", has_music_decoder("FLAC"), "FLAC decoder missing");
        CHECK_REASON("Mix decoder: MIDI", has_music_decoder("MIDI") || has_music_decoder("MID"), "MIDI decoder missing");
        CHECK_REASON("Mix decoder: WavPack", has_music_decoder("WAVPACK"), "WavPack decoder missing");
    }

    Mix_CloseAudio();
}

/* ------------------------------------------------------------------ */
/* SDL2_ttf                                                            */
/* ------------------------------------------------------------------ */

static void test_sdl2_ttf(void)
{
    printf("\n=== SDL2_ttf ===\n");

    int rc = TTF_Init();
    CHECK("TTF_Init (FreeType + HarfBuzz)", rc == 0);

    const SDL_version *ttf_ver = TTF_Linked_Version();
    printf("  SDL_ttf version: %d.%d.%d\n", ttf_ver->major, ttf_ver->minor, ttf_ver->patch);
}

/* ------------------------------------------------------------------ */
/* SDL2_gfx                                                            */
/* ------------------------------------------------------------------ */

static void test_sdl2_gfx(void)
{
    printf("\n=== SDL2_gfx ===\n");

    SDL_Window *window = SDL_CreateWindow(
        "native-smoke-gfx",
        SDL_WINDOWPOS_UNDEFINED,
        SDL_WINDOWPOS_UNDEFINED,
        64,
        64,
        SDL_WINDOW_HIDDEN);
    CHECK("SDL_CreateWindow (gfx headless)", window != NULL);

    if (window == NULL)
    {
        return;
    }

    SDL_Renderer *renderer = SDL_CreateRenderer(window, -1, SDL_RENDERER_SOFTWARE);
    CHECK("SDL_CreateRenderer (gfx headless)", renderer != NULL);

    if (renderer != NULL)
    {
        int draw_result = filledCircleRGBA(renderer, 32, 32, 12, 255, 255, 255, 255);
        CHECK("SDL2_gfx filledCircleRGBA", draw_result == 0);
        SDL_RenderPresent(renderer);
        SDL_DestroyRenderer(renderer);
    }

    SDL_DestroyWindow(window);
}

/* ------------------------------------------------------------------ */
/* SDL2_net (optional; gated on SMOKE_INCLUDE_NET so flaky upstream ports
 * on a given triplet do not block the rest of the native smoke harness)   */
/* ------------------------------------------------------------------ */

#ifdef SMOKE_INCLUDE_NET
static void test_sdl2_net(void)
{
    printf("\n=== SDL2_net ===\n");

    int rc = SDLNet_Init();
    CHECK("SDLNet_Init", rc == 0);

    const SDL_version *net_ver = SDLNet_Linked_Version();
    printf("  SDL_net version: %d.%d.%d\n", net_ver->major, net_ver->minor, net_ver->patch);
}
#endif

/* ------------------------------------------------------------------ */
/* Interactive mode — visual/audio test                                */
/* ------------------------------------------------------------------ */

#ifdef SMOKE_INTERACTIVE
static void test_interactive(void)
{
    printf("\n=== Interactive Mode ===\n");

    if (SDL_Init(SDL_INIT_VIDEO) != 0) {
        test_fail("SDL_Init(VIDEO)", SDL_GetError());
        return;
    }

    SDL_Window *win = SDL_CreateWindow(
        "Janset.SDL2 — Native Smoke Test",
        SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED,
        800, 600, SDL_WINDOW_SHOWN);
    CHECK("SDL_CreateWindow", win != NULL);

    if (!win) return;

    SDL_Renderer *renderer = SDL_CreateRenderer(win, -1, SDL_RENDERER_ACCELERATED);
    CHECK("SDL_CreateRenderer", renderer != NULL);

    if (renderer) {
        /* Draw something with SDL2_gfx to prove it works */
        SDL_SetRenderDrawColor(renderer, 30, 30, 46, 255);
        SDL_RenderClear(renderer);
        filledCircleRGBA(renderer, 400, 300, 100, 137, 180, 250, 255);
        stringRGBA(renderer, 300, 420, "Janset.SDL2 Hybrid Static - All satellites OK", 200, 200, 200, 255);
        SDL_RenderPresent(renderer);
    }

    printf("  Window open — press any key or wait 3 seconds...\n");
    SDL_Delay(3000);

    if (renderer) SDL_DestroyRenderer(renderer);
    SDL_DestroyWindow(win);
    test_ok("Interactive window lifecycle");
}
#endif

/* ------------------------------------------------------------------ */
/* Linux MIDI bridge                                                   */
/* ------------------------------------------------------------------ */

/* SDL_mixer's bundled internal Timidity (enabled via SDL2MIXER_MIDI_TIMIDITY in
 * our overlay port) searches for a config file at init time and only registers
 * the MIDI decoder if one is found. On Debian/Ubuntu the `timidity` apt package
 * installs the config at /etc/timidity/timidity.cfg, but SDL_mixer's default
 * probe path is /etc/timidity.cfg — so apt-install alone does not register the
 * MIDI decoder, and Mix decoder: MIDI reports "decoder missing" even though
 * every prereq is technically present.
 *
 * Bridge the gap by setting TIMIDITY_CFG to the Debian path when (a) the caller
 * has not already set it explicitly, and (b) the Debian cfg actually exists.
 * If timidity was not installed the env var stays unset and the test reports a
 * clear "decoder missing" failure — no silent fallback that masks the gap.
 *
 * Scope is Linux only: macOS has native MIDI via CoreAudio and Windows has
 * winmm, neither of which depends on this path. */
static void ensure_linux_timidity_cfg(void)
{
#ifdef __linux__
    const char *existing = getenv("TIMIDITY_CFG");
    if (existing != NULL && existing[0] != '\0')
    {
        return;
    }

    const char *debian_cfg = "/etc/timidity/timidity.cfg";
    FILE *probe = fopen(debian_cfg, "rb");
    if (probe == NULL)
    {
        return;
    }
    fclose(probe);

    setenv("TIMIDITY_CFG", debian_cfg, 0);
#endif
}

/* ------------------------------------------------------------------ */
/* Main                                                                */
/* ------------------------------------------------------------------ */

int main(int argc, char *argv[])
{
    (void)argc; (void)argv;

    ensure_linux_timidity_cfg();

    printf("Janset.SDL2 Native Smoke Test\n");
    printf("Mode: %s\n",
#ifdef SMOKE_INTERACTIVE
        "INTERACTIVE (real display + audio)"
#else
        "HEADLESS (dummy drivers, CI-safe)"
#endif
    );

    /* Core + all satellites (net optional per SMOKE_INCLUDE_NET). */
    test_sdl2_core();
    test_sdl2_image();
    test_sdl2_mixer();
    test_sdl2_ttf();
    test_sdl2_gfx();
#ifdef SMOKE_INCLUDE_NET
    test_sdl2_net();
#endif

#ifdef SMOKE_INTERACTIVE
    test_interactive();
#endif

    /* Cleanup */
#ifdef SMOKE_INCLUDE_NET
    SDLNet_Quit();
#endif
    TTF_Quit();
    Mix_Quit();
    IMG_Quit();
    SDL_Quit();

    /* Summary */
    printf("\n=== Summary ===\n");
    printf("  Passed: %d\n", g_pass);
    printf("  Failed: %d\n", g_fail);
    printf("  Result: %s\n", g_fail == 0 ? "ALL PASS" : "SOME FAILURES");

    return g_fail;
}
