# Xmana Xbox Deployment

Verified against Microsoft's Xbox/UWP documentation in September 2026. Re-check before each release.

## 1. Required tools

- Windows 10/11 PC with **Visual Studio 2022** and workloads: *Universal Windows Platform development* and *.NET desktop development*.
- Windows 10 SDK 10.0.19041 (target) / 10.0.17763 (minimum version).
- An Xbox in **Developer Mode** (requires a Partner Center developer account).

## 2. Developer-mode prerequisites

1. Activate Developer Mode on the console (Dev Home), let it restart.
2. Note the console IP shown in Dev Home.
3. Pair Visual Studio with the console (PIN shown on first deploy).

## 3. Build and run (development)

1. Open `Xmana.sln`.
2. Set `Xmana.App` as startup project, configuration **Debug**, platform **x64** (Xbox One and Series consoles are x64).
3. Project properties > Debug: Target device **Remote Machine**, enter the console IP, Authentication Mode **Universal (Unencrypted Protocol)**.
4. Press F5.

## 4. Unit tests (any machine with the .NET 8 SDK)

```text
dotnet test tests/Xmana.Tests/Xmana.Tests.csproj
```

## 5. Release packaging (later milestone)

Associate the app with the Store (*Project > Publish > Associate App with the Store*) to replace the development
`Identity` placeholders in `Package.appxmanifest`, then *Create App Packages*. Final packaging steps are completed in milestone 10.

## Common failures

- *App stops immediately / "unable to activate"*: confirm platform is x64 and the console is in Dev Mode.
- *Focus appears as a cursor*: `RequiresPointerMode` must be set in `App()` before window content is created.


## 6. Release packaging (Milestone 10)

1. Confirm the version has been bumped past the development placeholder (`Properties/AssemblyInfo.cs` `AssemblyVersion`/`AssemblyFileVersion` and `Package.appxmanifest` Identity `Version` must all agree — `tools/validate.py` fails the build otherwise).
2. *Project > Publish > Associate App with the Store* to replace the development `Identity Name`/`Publisher` placeholders with the real Store identity.
3. *Project > Publish > Create App Packages*, targeting x64 only (every supported console — Xbox One, One S, One X, Series S, Series X — is x64; see PROMPT.md section 78).
4. Submit through the Xbox Creators Program / Partner Center, or sideload the signed package directly via Dev Home for private distribution.
5. Keep `Capabilities` in `Package.appxmanifest` to the minimum the app actually uses (`internetClient` only, at the time of writing) — review this list on every release.

## 7. Platform capability notes (PROMPT.md section 78)

Xmana targets the weakest console in the lineup (original Xbox One) for every default:
- The default playback quality (`QualitySelector.DefaultHeight`, 720p) is deliberately conservative rather than assuming every console can decode the highest quality the API offers.
- No codec, resolution, or HDR capability is assumed in advance: the public API's `transcoddedFiles` response does not document codec or HDR metadata per quality (see docs/api-notes.md), so there is nothing reliable to pre-check. Instead, `PlaybackSession` detects an incompatible stream the only verifiable way available — a real decode failure from the platform `MediaPlayer` (`PlayerFailure.InvalidMedia`/`Decoding`) — and falls back through the other available qualities automatically (see "Recovery" in the Milestone 6 section above).
