# Xmana Architecture

Xmana is a native **UWP (XAML + C#)** app. This is the Microsoft-supported native stack for Xbox:
Windows App SDK / WinUI 3 does not support Xbox, and WebView2 is not available there. The app is
packaged as MSIX/AppX and runs in Xbox Dev Mode (or via the Creators Program).

## Layers

```text
UI (Xmana.App: pages, controls, theme)
  -> ViewModels / presentation state      (milestone 3+)
  -> Repositories                         (milestone 2+)
  -> Services / API / Database / Cache    (milestone 2+)
Xmana.Core: platform-neutral logic (no Windows.* types), unit-tested off-device
```

## Milestone 1 contents

| Area | Where | Notes |
|---|---|---|
| Branding | `assets/logo/generate_logo.py` | Single geometric source for the wordmark. Emits SVG masters, `Branding/Wordmarks.xaml` (vector, three variants), and tile/splash PNGs rendered from vectors (never upscaled). |
| Theme | `Xmana.App/Theme/` | Colors, typography, `ChoiceButton` style. TV-safe neutral dark palette, one accent. |
| Localization | `Xmana.Core/Localization`, `Services/LocalizationService.cs`, `Strings/{en-US,ar}` | Runtime language switch, RTL/LTR via `FlowDirection`, preference persisted in LocalSettings. English and Arabic share an identical key set (enforced by `tools/validate_milestone1.py`). |
| Navigation | `Xmana.Core/Navigation` | Pure state machine. B on pushed screen pops; B on a non-Home root goes Home; B on Home root is left to the system. |
| Focus | `Xmana.App/Focus/XmanaFocus.cs`, `Xmana.Core/Focus/FocusKeys.cs` | Elements carry stable string keys; focus is saved/restored by key, so it survives page re-creation and language changes. XY focus navigation enabled (`RequiresPointerMode=WhenRequested`). |
| Shell + splash | `Xmana.App/Shell/` | Navigation rail + content frame. Splash is solid black + centred wordmark, one 240 ms fade, no fake loading. Honors `MotionSettings` for Reduce Motion. |

## Milestone 2 contents

| Area | Where | Notes |
|---|---|---|
| HTTP layer | `Xmana.Core/Http` | `ApiHttpClient`: host allow-list, prioritised concurrency limit (4), per-attempt timeout, bounded GET retry with backoff, status mapping, JSON parse. Only `ApiException` or cancellation escape. |
| JSON | `Xmana.Core/Json` | Tolerant readers over `JToken` (no reflection model binding, safe with .NET Native). Numbers-as-strings, nulls, missing and wrong-typed fields never throw. |
| Models and mapping | `Xmana.Core/Models`, `Api/Mapping` | Immutable domain models; the raw JSON object is kept on each. Items without an id are dropped. |
| Endpoints | `Api/CinemanaEndpoints.cs` | One method per documented endpoint, each with its own pagination/sort semantics. |
| API client | `Api/ICinemanaApi.cs`, `CinemanaApi.cs` | Every call returns `ApiResult<T>` (value + origin: network, fresh cache, stale cache, cache after error). |
| Cache | `Xmana.Core/Caching` | Memory + disk tiers, per-kind TTL / stale-while-revalidate / error fallback, corruption recovery, size-pruned, signed URLs stripped before writing. |

## Milestone 3 contents (Home)

| Area | Where | Notes |
|---|---|---|
| Home state | `Xmana.Core/Home/HomeController.cs` | Loads hero, featured, latest movies/series, groups, newly added and popular concurrently by request priority. Each section succeeds or fails alone; empty sections are hidden; a refresh keeps what is on screen; stale cache data is swapped quietly after the background refresh (debounced); load-more per rail with its own pagination model. |
| Data source | `Home/IHomeDataSource.cs` | Narrow interface over `ICinemanaApi` so Home logic is unit-tested without the whole API. |
| Images | `Xmana.Core/Images`, `App/Services/ImageLoader.cs` | Picks the right API image field per role (plain fields before signed `*ObjUrl`), HTTPS-only download with size/type checks, hash-named disk cache (no URLs on disk), LRU memory cache of decoded bitmaps at a capped pixel width, in-flight de-duplication, cancellation, short negative cache. |
| UI | `Controls/VideoCard`, `RailView`, `HeroView`, `Pages/HomePage` | Posters load lazily by viewport distance and are released when far away. Hero has Details plus previous/next banner buttons (no key hijacking, so D-pad left still reaches the rail). Skeleton, empty, error (Retry) and offline notice states. Back from Details restores focus to the same card. |
| Navigation | `NavigationDestination.Details` | Pushed screen, not a rail item; it keeps the originating section highlighted. The Details content itself arrives in milestone 5. |

Not on Home yet (by design, not faked): Continue Watching (milestone 7, local data), Recommended (milestone 5), Play from the hero (milestone 6). There is no featured-series source in the documented API, so none is shown.

## Milestone 4 contents (Movies, Series, Anime, Categories, Search)

| Area | Where | Notes |
|---|---|---|
| Results engine | `Xmana.Core/Discovery/ResultsController.cs` | One growing, de-duplicated list with loading / empty / error states, load-more (with a retry that keeps the shown items), superseded requests dropped, offline flag. Every screen below uses it. |
| Browse | `BrowseController`, `BrowseModels` | Picks the endpoint from the query and keeps ITS pagination model: no category -> generic listing (page); category -> `videosByCategory` (offset); category + language -> `videosByCategoryAndLanguage` (offset). Only sort values with an established meaning are offered (default/reverse/highest/lowest rated). |
| Anime | `AnimeDefinition` | First-class section from existing metadata: category 57, defaulting to Japanese (language 22) only when the catalog offers it for that kind; otherwise plain category 57. Observed live ids, not official constants. |
| Categories | `CategoryCatalog`, `CategoryCatalogLoader`, `CategoryCatalogProvider` | Joins `mainCategories`, `subCategories` and `categoryLangFreqVideo`; languages shown per kind from real counts; failing language metadata just hides the language filter; shared and cached per UI language. |
| Search | `SearchController` | Debounced typing (450 ms), latest text wins, identical query not re-sent, Enter searches immediately, clearing returns to idle. Pages of 12, zero-based. |
| Advanced search | `AdvancedSearchController` | Sends only selected filters; empty or invalid selections are never sent; sort and exact staff matching are not promised (documented as unreliable). |
| UI | `Controls/ResultsView`, `Pages/BrowsePage`, `CategoriesPage`, `SearchPage`, `AdvancedSearchPage` | Rows are computed from the available width; posters load lazily. Controllers are kept per navigation entry (`PageSessions`), so Back from Details returns to the same list, pages and focused card. |

Search history and recent searches are local data and arrive with milestone 7. The grid is not virtualized yet; if profiling on hardware shows it is needed it belongs to milestone 8.

## Milestone 5 contents (Details, series, episodes, cast, recommendations)

| Area | Where | Notes |
|---|---|---|
| Details state | `Xmana.Core/Details/DetailsController.cs` | Metadata (`allVideoInfo`) decides whether the screen works; seasons/episodes, related titles, recommendations and the parent series title load independently and may fail or be empty without affecting the rest. A failed refresh keeps what is shown. |
| Series | `SeriesContext` | Seasons from `videoSeasonNumber` plus episodes from `videoSeason`, grouped by `season` and ordered by `episodeNummer`; previous/next episodes cross season boundaries. Episodes always use the **root series** id (`rootSeries`), never the episode id. |
| Related | `DetailsController` | `relatedVideos` is called with the video's own kind first and falls back to the one verified value (`kind/1`) when that returns nothing. The current video is removed. |
| Recommendations | `DetailsController` | Verified request body; only items that are real video objects are shown; failures are silent because the service is optional and the related rail remains. |
| Staff | `StaffController`, `StaffPage` | Person plus associated titles (no pagination is documented, so no load-more). |
| Duration | `DurationFormat` | The API's `duration` unit is undocumented, so only clock-style values are shown, as sent; bare numbers are never converted. |
| UI | `Pages/DetailsPage`, `StaffPage`, `Controls/StaffCard` | Poster, title, episode line, year/rating/duration, categories, language, synopsis, season selector and episode list, cast/directors/writers rails, recommended and related rails. From an episode, Previous/Next move sideways with `Replace` (the Back stack does not grow). |
| Navigation | `NavigationController.Replace`, `NavigationDestination.Staff` | Staff is a pushed screen like Details; both keep the originating rail section highlighted. Screens opened by Push/Replace set their own focus when ready instead of sending focus to the rail. |

Absent on purpose until their milestones: Play/Resume/trailer (milestone 6), favorites/watchlist/progress (milestone 7), comments and ratings display (comments are an unverified schema; ratings need a UI decision with the player). Nothing is shown as a placeholder for them.

## Milestone 6 contents (Player)

| Area | Where | Notes |
|---|---|---|
| Session | `Xmana.Core/Playback/PlaybackSession.cs` | All player rules, platform-neutral and unit-tested against a fake engine: preparation (qualities required; subtitles and skip data optional), resume/start over, transport, accelerated seeking, periodic progress writes, recovery, quality and subtitle selection, next-episode offer. |
| Quality | `QualitySelector` | Chosen only from the qualities the API returned for THIS video (stored preference, else best up to 720p, else the lowest). Never assumes five qualities. |
| Recovery | `PlaybackSession` | A failure is retried once on the same quality with a freshly requested address (signed addresses expire), then falls back through other qualities (lower first), always resuming at the last position; unplayable streams skip the retry; never loops forever; a manual Retry starts over. |
| Progress | `IProgressStore`, `FileProgressStore`, `ProgressPolicy` | Local JSON, ids/positions/timestamps only (never an address), bounded to 500 entries, atomic writes, corrupt file treated as empty. Written every 15 s while playing, on pause, on exit, on end and when the app is suspended; not written for the first few seconds; completed near the end (95% or last 20 s). Resume is offered only for a meaningful position. |
| Next episode | `AutoNextController` | Offer in the last 25 s; the countdown is the remaining playback time, so it reaches zero exactly when the media ends; Cancel; autoplay preference respected (default on, always cancellable). |
| Skip intro | `SkipController` | **Dormant by design**: the unit of `skippingDurations` was never established (all tested responses were empty). `SkipController.VerifiedUnit` stays `Unknown` until a populated response is verified, and while unknown no skip control is ever shown. The logic (seconds / milliseconds, range, server flag) is implemented and tested. |
| Subtitles | `TranslationInfo`, `SubtitleTrack.TryGetUri` | A track is offered only when its file is an absolute HTTPS URL; empty files and the placeholder path are never tracks. API.md does not document whether files are URLs or relative paths, so relative values are treated as unusable rather than guessed. The viewer's choice is remembered and applied only if that language exists. |
| Engine | `App/Services/MediaPlayerEngine.cs` | Wraps the platform `MediaPlayer`; subtitles are external timed-text sources shown by the platform. Addresses go straight to the platform and are never logged or stored. |
| UI | `App/Pages/PlayerPage` | Full screen (the shell hides the rail). Controls hidden: A play/pause, Left/Right seek (accelerating), Up/Down show the controls; shown: D-pad moves between buttons. B closes the options, hides the controls, cancels a next-episode offer, and only then leaves (`IBackHandler`). Resume/Start over prompt, loading/buffering/reconnecting indicator, failure screen with Retry, quality/subtitle/autoplay/episode options, keeps the screen awake while playing. |

Not in this milestone: trailers (the `trailer` field format is undocumented), thumbnail scrubbing previews (`video_thumbnails` was empty in every test), subtitle styling and the settings screen (milestone 9), and any hardware verification of HLS/MP4 behaviour (see docs/xbox-deployment.md).

## Milestone 7 contents (local features and Settings)

All of this is local to the device: no account, no sync, no upload (PROMPT.md section 33).

| Area | Where | Notes |
|---|---|---|
| Local library | `Xmana.Core/Library/LocalLibrary.cs` | Favorites, Watchlist, title snapshots and search history in one small JSON file (`library.json`): bounded lists (500), bounded snapshots (800, favorites never pruned), atomic writes, corrupt or foreign file treated as empty, list entries without a snapshot dropped. Snapshots keep only plain fields; signed `*ObjUrl` addresses are never stored. An unchanged snapshot causes no write and no notification. |
| Continue Watching | `ContinueWatching`, `HomeController` | Resumable, unfinished progress joined with remembered titles, newest first; for a series only the latest episode, and a newer finished episode hides older ones. A Home rail with progress bars that needs no network, so Home stays useful offline. Refreshed whenever Home is shown again. |
| History | `WatchHistory`, `HistoryLoader`, `HistoryPage` | Everything watched (finished or not) from the local progress records; titles watched before the library existed are fetched once (at most 12 per visit, cached) and remembered; unloadable titles are left out. |
| Search history | `LocalLibrary` | Normalised (trim, collapsed spaces), case-insensitive duplicates merged, newest first, capped at 20, 8 shown. Saved on Enter or when a result is opened (not on every debounced keystroke). Shown as recent searches on the idle Search screen, with Clear. |
| Favorites / Watchlist | `LibraryListPage`, Details | Toggles on movie and series details (an episode is added through its series). Lists update live. |
| Settings | `SettingsPage`, `PlayerPreferences`, `StorageService` | Language and Reduce Motion; preferred quality, subtitles, autoplay, offer-to-resume and skip-intro toggles; storage sizes with Clear images / Clear saved data / Clear search history / Clear watch history; Reset local data (everything personal plus caches and preferences, keeping the interface language); About (version, unofficial-client notice, open-source note, FAQ). Destructive actions use a confirmation panel with Cancel focused first; B cancels. |
| FAQ | `FaqReader`, `FaqPage` | The entry schema of `/faqQuestion` was never captured (API.md section 17 item 6), so entries are read defensively (properties named like question/answer, interface-language variant preferred); if nothing is readable the screen says the FAQ is unavailable. Update `FaqReader` once a real response is recorded. |

Skip Intro remains dormant (unit unverified) even when its setting is on; the setting only decides whether it may appear once the unit is verified.

## Milestone 8 contents (performance)

| Area | Where | Notes |
|---|---|---|
| Image memory budget | `ImageLoader`, `Xmana.Core/Caching/LruCache.cs` | Decoded bitmaps are kept in an LRU cache bounded by BOTH count (160) and actual decoded bytes (48 MB, 4 bytes/pixel), not by file size, so memory use has a real ceiling regardless of how many large posters were seen. |
| Image download concurrency and priority | `ImageDownloader`, `PriorityConcurrencyLimiter` | At most 4 simultaneous image downloads; the hero banner wins scarce slots over visible poster cards, which win over off-screen prefetch. Reuses the same priority limiter as the API client. |
| Disk cache housekeeping | `FileResponseCache.PurgeOlderThanAsync`, `ImageDiskCache.PurgeOlderThanAsync`, `App.xaml.cs` | Once per launch, off the UI thread, non-blocking: files older than 31 days are removed (nothing is ever trusted that long anyway; see the per-kind TTLs in `CachePolicy`). A failure here is invisible to the user. |
| Request prioritization | Already in place since milestone 2 (`PriorityConcurrencyLimiter`, `RequestPriority`) | Re-verified here: hero > continue-watching > primary rails > secondary rails > low-priority background work (FAQ, collections). |
| Grid row math | `Xmana.Core/Layout/GridWindow.cs` | Pure, tested row/offset arithmetic for a windowed poster grid (which rows to realize for a given scroll position). **Not wired into the UI yet** — see below. |

### Deferred: full UI virtualization of large poster grids
A hand-built virtualizing panel (only realizing rows near the viewport) risks breaking Xbox D-pad/gamepad focus navigation: WinUI's XY-focus engine can only move focus to, and auto-scroll toward, elements that already exist, so de-realizing a row the viewer is about to focus could silently stop navigation instead of scrolling to it — exactly the kind of regression PROMPT.md's "Validate controller input" step after every milestone is meant to catch. `GridWindow` is implemented and tested so this can be wired in safely once it is verified on real Xbox hardware with a gamepad (the "weak-hardware testing" step of this milestone, which needs a physical console and cannot be done in this sandbox); until then, posters already avoid the dominant memory cost (decoded bitmaps, see above), and rail/grid cards still release their images when scrolled far away (`VideoCard`/`StaffCard` `EffectiveViewportChanged`).

## Milestone 9 contents (polish)

| Area | Where | Notes |
|---|---|---|
| Modal focus trapping | `Controls/ConfirmPanel.xaml.cs`, `Pages/PlayerPage.xaml.cs` | PROMPT.md section 52 requires it explicitly. A `FocusManager.GettingFocus` handler cancels any focus move that would land outside the open modal (confirmation prompts, the player's options panel), because a semi-transparent scrim alone does not stop Xbox's spatial (XY) focus navigation from reaching controls underneath it. The player's options panel, failure screen and resume prompt also collapse the transport chrome (instead of only covering it) and disable the invisible input-sink button, so there is nothing else on screen to focus. |
| Player Error actions | `PlaybackSession.SelectQualityAsync`, `Pages/PlayerPage` | PROMPT.md section 59 specifies Retry / Change Quality / Go Back as three independent ways out of a failure. Change Quality now works directly from the failure screen (it was previously only reachable after a successful Retry): choosing a quality while `Failed` re-opens the stream at that quality from the last known position, the same way a plain Retry does. Offered only when more than one quality exists. |

### Reviewed, no change needed
- **Accessible names**: cards and icon-style buttons already carry `AutomationProperties.Name` (`VideoCard`, `StaffCard`, player transport buttons); labeled buttons use their visible label as their name.
- **Controller alternatives**: every player action reachable by remaining D-pad buttons (seek, play/pause, episode navigation) also has a shoulder-button or D-pad shortcut while the overlay is hidden.
- **Reduced motion**: already honored for the heavier animations (splash fade, hero backdrop fade, Home skeleton shimmer). The ~80-100 ms focus scale/opacity transitions on `ChoiceButton`/`VideoCard`/`StaffCard` are left as-is: they are not "long transitions" or "heavy parallax" and do not delay input, so they do not need the setting; disabling them would require per-instance control over the framework's built-in button focus-visual-state transitions for no real accessibility benefit.
- **Error-state wording**: PROMPT.md section 59 gives illustrative text ("Unable to load content. / Retry"); the app's actual copy conveys the same meaning per screen (see the `*_ErrorTitle`/`*_ErrorBody` resource keys) and was kept rather than replaced with the literal placeholder text.

## UI/UX redesign pass (post-milestone 9)

Applied against the uploaded redesign brief; scope kept to real, verifiable changes rather than a cosmetic pass.

| Change | Where | Notes |
|---|---|---|
| Top navigation header (was a left sidebar) | `Shell/ShellPage.xaml(.cs)`, `Theme/Controls.xaml` (`NavTabButtonStyle`) | The 10 sections are now a horizontal tab row below the wordmark; tabs size to their label (no fixed column). The active tab is a bottom underline instead of the old left-edge bar, which reads better in a horizontal layout; focus is still the same solid near-white plate used everywhere else in the app. `ChoiceButtonStyle` (used by Settings' option lists etc.) is untouched. |
| Strict monochrome palette | `Theme/Colors.xaml` | `XmanaAccentColor` changed from a blue hue (`#7EA6CF`) to a neutral light gray (`#D9D9D9`); every "accent" use (active-tab underline, poster progress fill, Advanced Search validation text) is now monochrome with no change needed at each call site. |
| Episode selection no longer reloads the page | `Xmana.Core/Details/DetailsController.SwitchToAsync`, `App/Pages/DetailsPage` | Selecting an episode (from the season list, or Previous/Next) now updates the existing Details screen in place — same controller instance, same page, no navigation — instead of pushing/replacing a whole new page. Previously shown content stays on screen while the new episode's metadata loads (no skeleton flash), matching how a background refresh already behaved. |

### Audited, no defect found
- **Hero banner stability**: `HomeController` already keeps the hero banner in its own slot, independent of Continue Watching (a separate rail); `HomePage.xaml`'s layout has no fixed spacer that would leave a gap above the hero when the offline notice is hidden (a `Collapsed` element takes no space in a `StackPanel`). No change made here; flag if a visual gap is still seen on-device so it can be pinned down with a screenshot.
- Favorites, Watchlist, subtitles, resume and quality selection were all already implemented (milestones 6-7) and are unaffected by this pass.

## Milestone 10 contents (release)

| Area | Where | Notes |
|---|---|---|
| Version | `Properties/AssemblyInfo.cs`, `Package.appxmanifest` | Bumped from the `0.1.0.0` development placeholder to `1.0.0.0`; `tools/validate.py` now fails if the two ever disagree or if the placeholder reappears. |
| Final cleanup | `tools/validate.py` | New gate: no leftover `TODO`/`FIXME`/`HACK` marker comments, no stray `Console.WriteLine` (logging goes through `IXmanaLogger` only, which redacts URLs), no `Debug.Assert` (silently compiled out in Release and never a correctness guard). Audited the full source tree; nothing needed removing. |
| Deployment documentation | `docs/xbox-deployment.md` | Added a release-packaging section (Store association, package creation, capability review) and a platform-capability note explaining why quality selection is reactive (real decode-failure fallback) rather than a guessed pre-check, per PROMPT.md section 78. |
| Testing | `tests/Xmana.Tests` | Re-checked against PROMPT.md section 77's checklist: API (valid/empty/malformed/missing fields/timeout/HTTP errors/cancellation), search (normal/Base64URL/special characters/empty), playback (play/pause/resume/seek/subtitles/quality/completion/next episode/network interruption) and local storage (add/remove/update/corruption recovery/reset) are all covered in `Xmana.Core` unit tests. "Migration" has no real migration to test yet (the app has shipped only one on-disk format); a mismatched format version is already covered and is treated as "start empty, next save replaces it" rather than invented upgrade logic. Navigation D-pad movement, modal focus trapping and player-overlay focus are UI/gamepad behavior that this headless sandbox cannot execute; they are covered by the manual validation steps in `docs/xbox-deployment.md` and must be re-checked on real hardware per PROMPT.md's "after every milestone" checklist. |

## Rules

- No literal UI text in XAML or C#; everything resolves through `LocalizationService`.
- `Xmana.Core` targets `netstandard2.0` and must not reference `Windows.*`.
- Platform-specific code stays in `Xmana.App`.
- The only declared capability is `internetClient`; the app talks to the three documented public hosts only.
- No screen calls the API yet; UI integration starts in milestone 3.
