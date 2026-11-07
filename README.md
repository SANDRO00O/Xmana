# Xmana

**Xmana — Movies, Series & Anime**

A native Xbox streaming application designed for:

- Xbox One
- Xbox One S
- Xbox One X
- Xbox Series S
- Xbox Series X

Xmana is designed as a premium, controller-first streaming experience with a cinematic dark interface, a custom playback UI, local-only personalization, and a clean architecture built around the documented public Cinemana/Shabakaty API.

> **Unofficial project:** Xmana is not affiliated with, endorsed by, or sponsored by Cinemana, Shabakaty, Microsoft, or Netflix.

## Repository Guide

- [PROMPT.md](PROMPT.md) — complete product and engineering specification for building Xmana.
- [API.md](API.md) — observed public API reference used by the application.
- [SECURITY.md](SECURITY.md) — security and responsible-use guidance.
- [docs/](docs/) — architecture, playback, Xbox deployment, troubleshooting, API verification, and research methodology.
- `src/` — application source code.
- `tests/` — automated tests.
- `assets/` — branding and application assets.

## Product Goals

Xmana should provide:

- Movies, series, and anime discovery.
- Search and advanced search.
- Categories, languages, groups, latest and featured content.
- Rich movie and series details.
- Seasons and episode navigation.
- A custom Xbox-friendly video player.
- Subtitle and quality selection when available.
- Resume playback, local history, favorites, and watchlist.
- Local metadata and image caching.
- Smooth controller-first navigation.
- Robust handling of empty responses, network failures, and playback errors.


## Languages

Xmana provides full first-class support for **Arabic and English**, including:

- Complete UI localization
- Arabic RTL and English LTR layouts
- Arabic/English search input
- Localized player controls and messages
- Localized settings, dialogs, errors, and accessibility labels
- Language preference persisted locally

Both languages must provide the same functional coverage. Arabic is not treated as a partial translation of the English interface.

## Data and Authentication Model

Xmana does **not** implement user login or account synchronization.

User-specific state is local to the Xbox installation. The project intentionally excludes authentication, account APIs, tokens, credentials, private user data, and authenticated state synchronization.

## API

Primary API base:

```text
https://cinemana.shabakaty.cc/api/android/
```

`API.md` is the source of truth for the documented public API behavior. It records observed endpoint paths, parameters, response shapes, pagination rules, sorting behavior, and known uncertainties.

Do not assume undocumented behavior. Do not hard-code temporary signed media URLs or credentials.

## Development

Read these files before implementation:

1. `PROMPT.md`
2. `API.md`
3. `SECURITY.md`

Build Xmana incrementally according to the milestones defined in `PROMPT.md`.

The implementation should use a current Microsoft-supported native Xbox application stack and should verify platform-specific APIs against current Microsoft documentation rather than assuming that desktop-only Windows APIs are available on Xbox.

## Status

| Milestone | State |
|---|---|
| 1. Project setup, branding, theme, navigation, focus, splash | Done: built in GitHub Actions |
| 2. Networking, API client, models, cache | Implemented; **not yet compiled/tested** (CI will build and run tests) |
| 3. Home (hero, rails, images, states) | Done: built in GitHub Actions |
| 4. Movies, Series, Anime, Categories, Search, Advanced Search | Done: built in GitHub Actions |
| 5. Details, series, seasons, episodes, cast, recommendations | Done: built in GitHub Actions |
| 6. Player, resume, subtitles, quality, next episode | Done: built in GitHub Actions (Skip Intro is dormant until its unit is verified) |
| 7. History, Favorites, Watchlist, Continue Watching, search history, Settings, FAQ | Done: built in GitHub Actions |
| 8. Performance (image memory budget, download concurrency, cache housekeeping) | Done: built in GitHub Actions. Full grid virtualization and weak-hardware testing need a physical Xbox and are deferred (see docs/architecture.md) |
| 9. Polish: modal focus trapping, Player Error actions | Done: built in GitHub Actions |
| UI/UX redesign pass: top nav bar, monochrome palette, in-place episode switching | Done: built in GitHub Actions |
| 10. Tests, packaging, deployment documentation, final cleanup, production configuration (v1.0.0.0) | Implemented; awaiting CI build/test |

### Build and test

- App: open `Xmana.sln` in Visual Studio 2022 (UWP workload) on Windows, build `Xmana.App` for x64, deploy to a Dev Mode Xbox. See [docs/xbox-deployment.md](docs/xbox-deployment.md).
- Core tests: `dotnet test tests/Xmana.Tests/Xmana.Tests.csproj`.
- CI: `.github/workflows/build.yml` runs static checks and tests on Ubuntu, then builds an unsigned x64 package on Windows (Debug and Release) and uploads it as an artifact.
- Static checks (no toolchain needed): `python3 tools/validate.py`.
- Regenerate logo assets: `python3 assets/logo/generate_logo.py` (needs Node + `sharp`).

## Repository Structure

```text
Xmana/
├── PROMPT.md
├── API.md
├── README.md
├── SECURITY.md
├── LICENSE
├── src/
├── tests/
├── assets/
├── docs/
└── tools/
```

## Licensing

The repository's documentation and original example material are provided under the [MIT License](LICENSE), unless a file states otherwise.

Third-party service APIs, trademarks, media, artwork, and other third-party material remain the property of their respective owners.
