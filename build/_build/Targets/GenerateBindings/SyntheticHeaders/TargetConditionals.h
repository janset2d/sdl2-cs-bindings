#pragma once
// Stub <TargetConditionals.h>: Apple toolchain header for TARGET_OS_IPHONE /
// TARGET_OS_MAC etc. SDL uses these for finer-grained Apple platform splits.
// We coarse-gate via __MACOSX__ / __IPHONEOS__ defines in PlatformCatalog; the
// finer Apple branches inside SDL collapse to harmless defaults under an empty
// stub.
