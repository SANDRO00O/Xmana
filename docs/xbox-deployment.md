# Xmana Xbox Deployment Notes

This document is intentionally platform-focused. The exact toolchain and package configuration must be verified against the current Microsoft Xbox documentation before implementation.

## Target devices

- Xbox One
- Xbox One S
- Xbox One X
- Xbox Series S
- Xbox Series X

## Development principle

Build and validate the weakest supported hardware first, especially the original Xbox One.

Do not assume that a desktop Windows API or media capability is available on Xbox.

## Deployment flow

```text
Source repository
    ↓
Local/CI build
    ↓
Package
    ↓
Install on Xbox development environment
    ↓
Controller + playback validation
```

## Validation checklist

- app launches
- navigation focus is stable
- D-pad/confirm/back work
- search can be used with controller input
- player opens and closes correctly
- playback survives pause/seek
- subtitle controls work when data exists
- quality controls reflect actual API data
- local progress survives navigation
- suspend/resume does not corrupt state
- errors remain recoverable

Do not claim a platform feature is available until it has been validated on the intended Xbox target.
