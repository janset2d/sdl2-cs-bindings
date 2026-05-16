#!/bin/bash
# Binding-generator container entrypoint.
#
# vcpkg state (vcpkg_installed/ + buildtrees/sdl2/src/) is baked into the image
# at build time by Layer E of binding-generator.Dockerfile. The container does
# not re-run vcpkg install at run time. If vcpkg.json / overlay-ports/* /
# overlay-triplets/* / build/manifest.json / the vcpkg submodule pin changes,
# Docker's layer cache invalidates the install layer automatically — no work
# required here.

set -euo pipefail

REPO_ROOT="${REPO_ROOT:-/workspace}"
RID="${RID:-linux-x64}"
BUILD_PROJECT="${REPO_ROOT}/build/_build"

echo "=== GenerateBindings (rid=${RID}) ==="
dotnet run --project "${BUILD_PROJECT}" --configuration Release -- \
    --repo-root "${REPO_ROOT}" \
    --target GenerateBindings \
    --rid "${RID}"
