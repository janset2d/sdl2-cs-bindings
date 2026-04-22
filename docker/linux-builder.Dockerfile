# syntax=docker/dockerfile:1.7
#
# Janset SDL2 bindings — Linux builder image.
#
# Base: ubuntu:20.04 (focal). glibc 2.31. Originally planned as debian:buster
# (glibc 2.28, SkiaSharp pattern) to maximize consumer reach; buster went EoL
# 2022-Aug and was moved to archive.debian.org in 2024-03, so `apt-get update`
# started returning 404s on Release files and `apt-get upgrade -y` became a
# no-op (no upstream security patches). Switched to ubuntu:20.04 focal on
# 2026-04-22: standard LTS support through 2030 (Pro ESM 2035), active apt
# mirrors, glibc 2.31 — same baseline as Ubuntu 20.04 / Debian 11 bullseye,
# which covers the bulk of the modern Linux ecosystem. The marginal consumer
# reach delta (RHEL 8 / AlmaLinux 8 / Debian 10 users on glibc 2.28) is
# accepted in exchange for live security patches on monthly rebuild.
#
# Slice E E2, per Deniz direction 2026-04-22: one image, multi-arch manifest
# list (amd64 + arm64 merged), built by
# `.github/workflows/build-linux-container.yml`, pushed to
# `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-<yyyymmdd>-<sha>` and
# pointed at by `:focal-latest`.
#
# The apt preamble is a verbatim port of
# `.github/workflows/prepare-native-assets-linux.yml:37-80`. That preamble
# survived several rounds of trial-and-error ON ubuntu:20.04 and is the
# canonical working set for vcpkg hybrid-static SDL2 + smoke runtime. Do NOT
# edit package lists or flags without updating the prepare-* workflow in
# lockstep — drift between the two surfaces is exactly what this image
# exists to eliminate.
#
# Two additions over the prepare-* preamble, each intentional:
#   (1) cmake + ninja-build — native-smoke CI needs them; prepare-* today runs
#       Harvest only so those tools aren't installed there. When Slice E E1c
#       points `manifest.runtimes[].container_image` at this image, NativeSmoke
#       jobs in release.yml pick them up from the image.
#   (2) `git config --system --add safe.directory '*'` — replaces per-job
#       `git config --global --add safe.directory "$(pwd)"` (prepare-* line 87).
#       Image-level config means Cake.Frosting.Git works under `container:`
#       without a per-job boilerplate step.
#
# .NET SDK deliberately NOT baked in — `actions/setup-dotnet` handles runtime
# version pinning per-job (symmetric with Windows / macOS runners).

FROM ubuntu:20.04

LABEL org.opencontainers.image.source="https://github.com/janset2d/sdl2-cs-bindings"
LABEL org.opencontainers.image.description="Janset SDL2 bindings Linux builder (ubuntu:20.04 base, glibc 2.31)"
LABEL org.opencontainers.image.licenses="MIT"
LABEL org.opencontainers.image.vendor="Janset2D"

ENV DEBIAN_FRONTEND=noninteractive

# ---------------------------------------------------------------------------
# Verbatim port of prepare-native-assets-linux.yml:37-80.
# Three apt-install groups + apt-cache libicu shim + autoconf 2.72 source
# build, ordered identically to the workflow step so regressions are easy to
# bisect across surfaces.
#
# `apt-get upgrade -y` is an image-only addition (not in prepare-*): the
# monthly rebuild cron absorbs upstream security patches without changing
# focal's glibc major (2.31.X → 2.31.Y patch bumps only; minor versions
# never leave the release suite per Ubuntu LTS policy). Same reason we
# stay on focal as base — consumer-side glibc 2.31 floor stays stable.
# The toolchain smoke check at the bottom of this file logs the resolved
# glibc version on every build so drift is visible in the image build log.
# ---------------------------------------------------------------------------
RUN apt-get update \
 && apt-get upgrade -y \
 && apt-get install -y --no-install-recommends \
      git wget curl build-essential pkg-config tar unzip zip ca-certificates \
      python3 python3-pip python3-venv python3-jinja2 bison autoconf automake \
      autoconf-archive libltdl-dev libtool \
 && apt-get install -y --no-install-recommends \
      libx11-dev libxext-dev libxrandr-dev libxinerama-dev libxcursor-dev \
      libxi-dev libxss-dev libxrender-dev \
      libwayland-dev libxkbcommon-dev wayland-protocols libegl1-mesa-dev libdrm-dev \
      libvulkan-dev libasound2-dev \
 && apt-get install -y --no-install-recommends freepats \
 && apt-get install -y --no-install-recommends \
      "$(apt-cache search '^libicu[0-9]+$' | head -1 | cut -d' ' -f1)" \
 && update-ca-certificates \
 && rm -rf /var/lib/apt/lists/*

# autoconf 2.72 source build — gperf 3.3 (transitive dep via harfbuzz) requires
# autoconf >= 2.70. ubuntu:20.04 ships 2.69 (same as prepare-*'s in-workflow
# preamble). if-check preserved from the workflow so if a future base bump
# lands autoconf >= 2.70 the source build becomes a no-op.
RUN SYSTEM_AUTOCONF_VER=$(autoconf --version 2>/dev/null | head -1 | grep -oP '\d+\.\d+' || echo "0.0") \
 && if [ "$(printf '%s\n' "2.70" "$SYSTEM_AUTOCONF_VER" | sort -V | head -1)" != "2.70" ]; then \
      AUTOCONF_SHA256="afb181a76e1ee72832f6581c0eddf8df032b83e2e0239ef79ebedc4467d92d6e"; \
      curl -fsSL -o /tmp/autoconf-2.72.tar.gz https://ftp.gnu.org/gnu/autoconf/autoconf-2.72.tar.gz \
      && echo "$AUTOCONF_SHA256  /tmp/autoconf-2.72.tar.gz" | sha256sum -c - \
      && tar xz -C /tmp -f /tmp/autoconf-2.72.tar.gz \
      && cd /tmp/autoconf-2.72 && ./configure --prefix=/usr && make -j"$(nproc)" && make install \
      && cd / && rm -rf /tmp/autoconf-2.72* \
      && echo "autoconf version now: $(autoconf --version | head -1)"; \
    fi

# ---------------------------------------------------------------------------
# Addition (1) — native-smoke toolchain (cmake + ninja-build). Not in
# prepare-native-assets-linux.yml because that workflow runs Harvest only.
# Slice E E1c release.yml native-smoke job needs these at container entry.
# ---------------------------------------------------------------------------
RUN apt-get update \
 && apt-get install -y --no-install-recommends cmake ninja-build \
 && rm -rf /var/lib/apt/lists/*

# ---------------------------------------------------------------------------
# Addition (2) — image-level safe.directory config. Replaces prepare-*'s
# per-job `git config --global --add safe.directory "$(pwd)"` step.
# ---------------------------------------------------------------------------
RUN git config --system --add safe.directory '*'

# ---------------------------------------------------------------------------
# Toolchain smoke check — logs the actual version of every pipeline-critical
# tool baked into the image. Any regression (base bump shipping a broken apt
# package, autoconf source tarball drift, cmake apt channel change) fails at
# image build time rather than downstream at Harvest / NativeSmoke. Also acts
# as self-documentation in the GHCR build log: operators can `docker pull`
# the tag and read the expected GCC / glibc / CMake baseline from here.
# ---------------------------------------------------------------------------
RUN echo "=== debian base ===" \
 && cat /etc/os-release \
 && echo "=== glibc ===" \
 && ldd --version | head -1 \
 && echo "=== compilers ===" \
 && gcc --version | head -1 \
 && g++ --version | head -1 \
 && echo "=== build tooling ===" \
 && cmake --version | head -1 \
 && ninja --version \
 && autoconf --version | head -1 \
 && automake --version | head -1 \
 && libtool --version | head -1 \
 && pkg-config --version \
 && echo "=== scripting / vcs ===" \
 && python3 --version \
 && git --version

WORKDIR /workspace
