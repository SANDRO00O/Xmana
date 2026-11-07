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
  - [docs/architecture.md](docs/architecture.md) — application architecture boundaries.
  - [docs/playback.md](docs/playback.md) — player behavior and local progress rules.
  - [docs/xbox-deployment.md](docs/xbox-deployment.md) — Xbox build/deployment validation notes.
  - [docs/troubleshooting.md](docs/troubleshooting.md) — known API/player/UI failure modes.
  - [docs/api-verification-matrix.md](docs/api-verification-matrix.md) — verified/uncertain/empty status for the API surface.
  - [docs/research-methodology.md](docs/research-methodology.md) — how the API was discovered and validated.
- `src/` — application source code.
- `tools/` — asset generator and repository validator.
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

`API.md` is the source of truth for the documented public API behavior. It records verified request bodies, endpoint paths, parameters, response shapes, pagination rules, sorting behavior, live backend quirks, and known uncertainties.

`docs/api-verification-matrix.md` and `docs/research-methodology.md` exist so an implementation agent does not ask the user to repeat already-completed API research.

Do not assume undocumented behavior. Do not hard-code temporary signed media URLs or credentials.

## Development

Read these files before implementation:

1. `PROMPT.md`
2. `API.md`
3. `SECURITY.md`

Build Xmana incrementally according to the milestones defined in `PROMPT.md`.

The implementation should use a current Microsoft-supported native Xbox application stack and should verify platform-specific APIs against current Microsoft documentation rather than assuming that desktop-only Windows APIs are available on Xbox.

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
└── docs/
```

## Licensing

The repository's documentation and original example material are provided under the [MIT License](LICENSE), unless a file states otherwise.

Third-party service APIs, trademarks, media, artwork, and other third-party material remain the property of their respective owners.


## Building

Requirements: Windows 10/11, Visual Studio 2022 with the *Universal Windows Platform development* workload
(Windows SDK 10.0.19041 or newer). Open `src/Xmana.sln`, choose `x64`, and build.

CI: `.github/workflows/build.yml` validates localization parity (Linux) and builds the UWP project
(`windows-latest`, `Release|x64`). Xbox deployment steps are in [docs/xbox-deployment.md](docs/xbox-deployment.md).

## Source layout

```text
src/Xmana.Core   netstandard2.0: localization, settings abstractions, logging (no UI dependencies)
src/Xmana.App    UWP app: shell, navigation, focus, theme, pages
tools/           asset generator and repository validator
```

Status: the build is milestone based. Milestone 1 (project, branding, theme, shell navigation, focus,
splash, Arabic/English with RTL) is implemented. Nothing in this repository has been verified on
physical Xbox hardware yet.
