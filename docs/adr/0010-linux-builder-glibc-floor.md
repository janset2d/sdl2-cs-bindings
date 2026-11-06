# Linux Builder glibc Floor

Date: 2026-10-06

The Linux builder base is Ubuntu 20.04 focal with glibc 2.31. This is a constitutional floor:
it is never raised and raising it is never proposed. Going lower is welcome when it needs no bad toolchain.

## Considered options

The [builder Dockerfile](../../docker/linux-builder.Dockerfile) records the original Debian buster /
glibc 2.28 plan. Buster's end of life in August 2022 and move to archived mirrors in March 2024
made that maintenance route unsuitable. Focal was adopted on 2026-04-22 for maintained mirrors and
the support horizon recorded there, through 2030. The reach loss for RHEL 8, AlmaLinux 8 and
Debian 10 consumers on glibc 2.28 was accepted in exchange for live security patches on monthly rebuilds.

## Consequences

The generator may run in a newer-glibc stage, but it parses canonical headers from the unchanged
builder. Its newer glibc must never reach consumer natives. Builder and generator runtime requirements
are separate contracts.

AlmaLinux 8 / glibc 2.28 is a research lane for a lower floor, not permission to raise this one.
The Dockerfile retains its self-contained local explanation.
