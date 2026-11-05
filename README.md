# Xmana

[![Build Xmana for Xbox Dev Mode](https://github.com/SANDRO00O/Xmana/actions/workflows/build-xbox.yml/badge.svg)](https://github.com/SANDRO00O/Xmana/actions/workflows/build-xbox.yml)

Xmana is a cinematic, controller-first media catalogue and playback client built with native UWP, XAML and C# for Xbox Developer Mode.

## Interface direction

This is a television UI, not a phone layout stretched to 16:9. Navigation lives in a horizontal top tab bar. The home screen uses a landscape feature panel and horizontal poster rails with generous focus targets, ten-foot typography, D-pad navigation and no side drawer.

## Project

- Xmana.sln — Visual Studio solution.
- src/Xmana — UWP x64 app.
- API.md — observed public Cinemana API reference used by the client.
- docs/implementation-status.md — implemented features and remaining work.
- docs/ — architecture, API verification, playback and Xbox deployment notes.
- .github/workflows/build-xbox.yml — x64 package build and development signing.

## Build

Use Visual Studio with the Universal Windows Platform workload and Windows 10 SDK 19041 or later. Run tools/Generate-Assets.ps1 once to create the manifest images, then open Xmana.sln, choose Release | x64, and build the package.

GitHub Actions runs the Windows build, creates a short-lived self-signed code-signing certificate, signs the generated MSIX using SHA-256, verifies the signature, and publishes the MSIX with its matching public .cer certificate. No private key is stored in the repository.

## Install on Xbox Developer Mode

Download the MSIX and Xmana-Dev-Certificate.cer from the same workflow artifact. In Xbox Device Portal, open Apps → Deploy apps, upload the package and select the matching certificate. Follow docs/xbox-deployment.md.

The certificate is for development/testing only. It is regenerated on each build, so keep each certificate with the package created in that workflow run.

## API scope and limitations

The client follows the public/read-oriented API paths and request shapes documented in API.md. Account login, subscriptions, notifications, personal history and other account-changing operations are intentionally excluded. Unknown response fields and signed media URL formats are not assumed; playback currently uses best-effort URL discovery until a stable live response shape can be verified.

## Status

This is an initial implementation milestone, not a feature-complete media product. The API client, TV shell, catalogue views, local library features, season navigation and MediaPlayerElement playback path are present. Real Xbox hardware testing, subtitle selection/rendering, playback recovery, caching, deeper collection browsing and full RTL review remain outstanding. See docs/implementation-status.md.

## License

See LICENSE.
