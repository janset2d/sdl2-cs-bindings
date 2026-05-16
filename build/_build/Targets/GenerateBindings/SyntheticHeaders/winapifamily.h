#pragma once
// Stub <winapifamily.h>: transitively pulled by SDL headers under __WINRT__ for
// WINAPI_FAMILY_PARTITION partition checks. We never enable any partition; the
// SDL macro paths gated by these checks fall through to the desktop branch which
// is what we want for a uniform Windows surface emit.
