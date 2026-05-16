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

# Layer E — vcpkg install. Bakes vcpkg_installed/ + buildtrees/<port>/src/ into
# the image. BuildKit cache mount accelerates subsequent rebuilds on the same
# machine; CI uses actions/cache instead (mount semantics don't carry to GHA).
RUN --mount=type=cache,id=janset-vcpkg-binarycache,target=/vcpkg-cache \
    VCPKG_DEFAULT_BINARY_CACHE=/vcpkg-cache \
    /workspace/external/vcpkg/vcpkg install \
        --triplet x64-linux-hybrid \
        --x-manifest-root=/workspace \
        --overlay-ports=/workspace/vcpkg-overlay-ports \
        --overlay-triplets=/workspace/vcpkg-overlay-triplets

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
