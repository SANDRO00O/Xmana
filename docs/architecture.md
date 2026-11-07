# Xmana Architecture Notes

`PROMPT.md` is the product/engineering specification. `API.md` is the public API source of truth.

Recommended runtime architecture:

```text
UI
 ↓
ViewModels
 ↓
Repositories
 ↓
 ├── Public API Client
 ├── Local Database
 ├── Cache
 └── Playback Services
```

Keep remote DTOs, domain models, database entities, and UI models separate.

The app must not bind controls directly to raw API JSON.

## Public API boundary

Only the read-oriented public surface documented in `API.md` belongs in the Xmana runtime API layer.

Account, authentication, telemetry, remote personal state, and account-changing endpoints are intentionally outside the runtime boundary.

## Failure isolation

A failed optional content rail, recommendation request, thumbnail service, or rating summary must not crash or block the rest of the application.

## Local state

Continue watching, history, favorites, watchlist, search history, and player preferences are local-only.

## Controller-first UI

Navigation and focus are first-class services. Every major action must remain reachable without a mouse.
