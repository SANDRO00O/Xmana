# Xmana Architecture Notes

API.md is the public API source of truth. Keep the runtime boundary limited to the read-oriented public surface.

## Runtime shape

- XAML page and reusable visual components
- CinemanaApiClient for documented network routes
- VideoItem as a tolerant UI-facing metadata model
- LocalStateStore for device-local preferences and favorites
- PlaybackService wrapping UWP MediaPlayerElement

This initial milestone keeps the service boundary small. As the app grows, separate remote DTOs, domain objects, persistence records and view models instead of binding UI controls to raw API JSON.

## Public API boundary

Account, authentication, telemetry, remote personal state and account-changing endpoints are outside the runtime boundary. Do not add them simply because frontend code references them.

## Controller-first UI

Navigation is a horizontal tab strip above the page canvas. Focus states must be obvious, deterministic and reachable with the Xbox controller. The layout is designed for a 16:9 television viewing distance, not scaled up from a phone layout.

## Failure isolation

A failed optional rail, recommendation request, thumbnail service or rating summary must not crash or block the rest of the catalog.

## Local state

Favorites and interface language currently remain on the local console. Watch progress, continue-watching and cached metadata are planned but not yet implemented.
