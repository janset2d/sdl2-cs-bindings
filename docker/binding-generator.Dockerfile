# syntax=docker/dockerfile:1.7
#
# Janset SDL2 bindings — Stage 1 binding generator image.
#
# Derived from linux-builder:focal-latest (sourced via ARG BASE_IMAGE from
# build/manifest.json runtimes[linux-x64].container_image). Bakes vcpkg state
# at image build time so the container is a clean consumer at run time:
#
#   * Layer A — .NET SDK
#   * Layer B — NuGet restore (csproj-only inputs)
#   * Layer C — vcpkg toolchain at submodule-pinned commit (VCPKG_COMMIT build-arg)
#   * Layer D — vcpkg manifest + overlays COPY (Layer E's full input set)
#   * Layer E — `vcpkg install` (bakes vcpkg_installed/ + buildtrees/<port>/src/
#               into the image; binary cache mount accelerates subsequent rebuilds)
#   * Layer F — full repo source COPY + entrypoint registration
#
# Layer cache invalidation discipline matches vcpkg's own ABI key:
#   - VCPKG_COMMIT bump  → Layer C re-runs (full vcpkg toolchain refresh)
#   - vcpkg.json change  → Layer D re-runs → Layer E re-runs (vcpkg rebuild)
#   - overlay change     → Layer D re-runs → Layer E re-runs
#   - manifest.json change → Layer D re-runs (RID/triplet metadata) → Layer E re-runs
#   - Repo code edit     → only Layer F re-runs (vcpkg state preserved)
#
# Output artifact lands via bind-mount at run time (not baked into image).
# vcpkg binary cache for run-time vcpkg invocations is not needed because the
# container does not re-run vcpkg install at run time — see entrypoint.

ARG BASE_IMAGE
FROM ${BASE_IMAGE}

# Layer A — .NET SDK via dotnet-install.sh, pinned to global.json.
COPY global.json /tmp/global.json
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && chmod +x /tmp/dotnet-install.sh \
 && /tmp/dotnet-install.sh --jsonfile /tmp/global.json --install-dir /usr/share/dotnet \
 && rm /tmp/dotnet-install.sh
ENV PATH="${PATH}:/usr/share/dotnet"
ENV DOTNET_ROOT=/usr/share/dotnet
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1

# CPATH env-var contract for libclang. NuGet libclang.runtime.linux-x64 ships
# only libclang.so — no clang resource directory, no default sysroot detection.
# libclang reads CPATH natively (same mechanism as the clang CLI). Paths:
#   1. /usr/lib/gcc/x86_64-linux-gnu/11/include : compiler-shipped headers
#      (stddef.h, stdint.h, stdarg.h, GCC SSE/AVX intrinsics — SDL2's
#      SDL_DISABLE_*_H defines short-circuit the intrinsic-include chain
#      so SDL_cpuinfo.h never reaches them under generator parses).
#   2. /usr/include/x86_64-linux-gnu : glibc multiarch headers.
#   3. /usr/include : generic system header tree.
ENV CPATH="/usr/lib/gcc/x86_64-linux-gnu/11/include:/usr/include/x86_64-linux-gnu:/usr/include"

# Layer B — NuGet restore. Rebuilds only when csproj / CPM / lock / global.json change.
WORKDIR /workspace
COPY Directory.Packages.props Directory.Build.props global.json /workspace/
COPY NuGet.config* /workspace/
COPY build/_build/Build.csproj /workspace/build/_build/
COPY build/_build/packages.lock.json /workspace/build/_build/
COPY build/_build.Tests/Build.Tests.csproj /workspace/build/_build.Tests/
COPY build/_build.Tests/packages.lock.json /workspace/build/_build.Tests/
RUN dotnet restore /workspace/build/_build/Build.csproj --locked-mode

# Layer C — vcpkg toolchain at submodule-pinned commit.
# tools.cs resolves the submodule commit via `git rev-parse HEAD:external/vcpkg`
# and passes it as VCPKG_COMMIT. Using a build-arg + git clone (rather than
# `git submodule update --init`) keeps the layer cache stable across repo
# commits that don't touch the vcpkg submodule — the common case. Submodule
# init would require .git/ in the build context, which invalidates on every
# parent-repo commit and defeats layer caching for vcpkg state.
ARG VCPKG_COMMIT
RUN test -n "${VCPKG_COMMIT}" \
 && git clone --filter=blob:none https://github.com/microsoft/vcpkg.git /workspace/external/vcpkg \
 && cd /workspace/external/vcpkg && git checkout "${VCPKG_COMMIT}" \
 && ./bootstrap-vcpkg.sh -disableMetrics

# Layer D — vcpkg manifest + overlays. These four COPYs define Layer E's input
# set. Changing any of them invalidates the vcpkg install layer.
COPY vcpkg.json /workspace/
COPY build/manifest.json /workspace/build/manifest.json
COPY vcpkg-overlay-ports/ /workspace/vcpkg-overlay-ports/
COPY vcpkg-overlay-triplets/ /workspace/vcpkg-overlay-triplets/

# Layer E — vcpkg install. Bakes vcpkg_installed/ + buildtrees/sdl2/src/ into
# the image. Only ONE BuildKit cache mount: the binary cache. The previous
# attempt at a buildtree cache mount was misguided — BuildKit cache mounts are
# RUN-scoped (the target path is mounted only during the RUN, then unmounted
# before the layer commits). Content written to a cache mount's target NEVER
# lands in the image layer. CI uses actions/cache@v5 with multi-path semantics
# (PSTH-I's .github/actions/vcpkg-setup/action.yml) — that works because actions/
# cache restores BEFORE the step runs and the restored content is on the normal
# filesystem. There is no Docker analogue.
#
# DYNAPI MANIFEST REACH:
# DynapiCoherenceValidator (post-emit Stage 1 validator) reads
# buildtrees/sdl2/src/<dir>/src/dynapi/SDL2.exports — that file is part of
# SDL2's extracted source tree. vcpkg writes the extracted source to
# buildtrees/sdl2/src/<hash>.clean/ ONLY when it builds SDL2 from source. On a
# binary-cache hit, vcpkg restores compiled artifacts to installed/ and skips
# extraction entirely — buildtrees stays empty.
#
# Two-step solution:
#   (1) vcpkg install runs against the binary cache (read+write). Cold cache →
#       full from-source build → buildtrees populated by vcpkg → ends up in
#       image layer naturally. Warm cache → fast restore → buildtrees stays
#       empty.
#   (2) Post-install conditional fallback: if buildtrees/sdl2/src/ is empty,
#       independently clone SDL2 upstream at the manifest-pinned version (passed
#       via SDL2_VERSION build-arg, sourced from manifest.library_manifests[
#       name=SDL2].vcpkg_version by tools.cs). Clone target sits under
#       buildtrees/sdl2/src/sdl2-dynapi-${SDL2_VERSION}/ — a single deterministic
#       subdir name so DynapiManifestRepository's `buildtrees/sdl2/src/*/src/
#       dynapi/SDL2.exports` glob still matches exactly one candidate (P1.8
#       single-match invariant).
#
# In the cold-vcpkg branch, vcpkg's own <hash>.clean/ already exists — the
# conditional `ls -A` check skips the clone, no P1.8 ambiguity. In the warm-
# binary-cache branch, the only subdir is our clone. Either way: single match,
# manifest reachable, image layer self-contained.
#
# BINARY CACHE INVALIDATION via VCPKG_CACHE_KEY:
# The binary cache mount id interpolates ${VCPKG_CACHE_KEY} — a SHA-256 prefix
# that tools.cs computes over vcpkg.json + vcpkg-overlay-ports/** +
# vcpkg-overlay-triplets/** + vcpkg submodule commit. Content changes yield a
# fresh key → new mount → cold from vcpkg's perspective → full from-source
# build → image layer captures buildtrees. Key composition mirrors
# .github/actions/vcpkg-setup/action.yml's actions/cache@v5 key exactly so
# local invalidation matches CI invalidation 1:1.
ARG VCPKG_CACHE_KEY
ARG SDL2_VERSION
RUN test -n "${VCPKG_CACHE_KEY}" && test -n "${SDL2_VERSION}" \
 && echo "[binding-generator] vcpkg cache key: ${VCPKG_CACHE_KEY}; SDL2 version: ${SDL2_VERSION}"
RUN --mount=type=cache,id=janset-vcpkg-binarycache-${VCPKG_CACHE_KEY},target=/vcpkg-cache \
    set -e ; \
    echo "[binding-generator] vcpkg install (binary cache: read+write)" ; \
    VCPKG_BINARY_SOURCES="clear;files,/vcpkg-cache,readwrite" \
    /workspace/external/vcpkg/vcpkg install \
        --triplet x64-linux-hybrid \
        --x-manifest-root=/workspace \
        --overlay-ports=/workspace/vcpkg-overlay-ports \
        --overlay-triplets=/workspace/vcpkg-overlay-triplets ; \
    if [ -z "$(ls -A /workspace/external/vcpkg/buildtrees/sdl2/src 2>/dev/null)" ] ; then \
        echo "[binding-generator] vcpkg binary cache hit skipped SDL2 source extraction — cloning independent SDL2 source at release-${SDL2_VERSION} for dynapi manifest reach" ; \
        SDL2_CLONE_DIR="/workspace/external/vcpkg/buildtrees/sdl2/src/sdl2-dynapi-${SDL2_VERSION}" ; \
        git clone --depth 1 --branch "release-${SDL2_VERSION}" https://github.com/libsdl-org/SDL.git "${SDL2_CLONE_DIR}" ; \
        test -f "${SDL2_CLONE_DIR}/src/dynapi/SDL2.exports" || (echo "[binding-generator] FATAL: SDL2 dynapi manifest missing in cloned source — upstream layout may have changed at release-${SDL2_VERSION}" && exit 1) ; \
    else \
        echo "[binding-generator] vcpkg from-source build already populated buildtrees — skipping independent SDL2 clone" ; \
    fi

# Smoke check — log resolved versions for audit trail on every image build.
RUN echo "=== dotnet ===" \
 && dotnet --version \
 && echo "=== vcpkg ===" \
 && /workspace/external/vcpkg/vcpkg version | head -1 \
 && echo "=== SDL2 dynapi manifest present ===" \
 && ls /workspace/external/vcpkg/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports

# Layer F — full repo source COPY. Rebuilds on every code change.
# .dockerignore excludes bin/, obj/, vcpkg_installed/, .vs/, artifacts/,
# .cake-host/, external/vcpkg/ (Layer C+E baked vcpkg state survives), .git/,
# .github/, .claude/.
COPY . /workspace
WORKDIR /workspace

RUN cp /workspace/docker/binding-generator-entrypoint.sh /usr/local/bin/binding-generator \
 && chmod +x /usr/local/bin/binding-generator

ENTRYPOINT ["/usr/local/bin/binding-generator"]
