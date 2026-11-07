# Xmana — Master Development Prompt

## Role

You are a senior Xbox application architect, native streaming-media engineer, C# developer, UI/UX designer, video-player engineer, and production software architect.

Build **Xmana**, a polished native streaming application for Xbox that provides a modern cinematic experience for watching:

- Movies
- TV series
- Anime

The application targets:

- Xbox One
- Xbox One S
- Xbox One X
- Xbox Series S
- Xbox Series X

The application should feel like a complete commercial streaming service rather than a prototype.

Use the current Microsoft-supported native Xbox application stack and verify platform-specific APIs against the current Microsoft documentation before implementation. Do not assume desktop-only Windows APIs are available on Xbox.

---

# 1. Product Identity

## Application Name

**Xmana**

Official product identity:

```text
Xmana
```

Suggested tagline:

```text
Xmana — Movies, Series & Anime
```

The application, project, namespace, documentation, UI, splash screen, settings, player branding, package metadata, and repository should consistently use the Xmana name.

Cinemana/Shabakaty is only the source of the public API.

Do not make Cinemana or Shabakaty the application's visual identity.

The application must clearly identify itself as an **unofficial client**.

---

# 2. Visual Identity and Logo

Create an original Xmana visual identity.

## Primary Logo

The primary logo must be extremely simple:

- Solid black background
- The word **Xmana** centered horizontally and vertically
- Clean modern typeface
- White or near-white lettering
- Strong contrast
- Premium cinematic appearance
- Excellent readability from a television viewing distance

Do not copy Netflix's logo or typography.

Do not use unnecessary symbols or decorative graphics.

The visual concept should communicate:

```text
Minimal
Cinematic
Modern
Premium
Clean
Xbox-friendly
```

## Logo Variants

Create a reusable logo system rather than one fixed bitmap.

### Full Wordmark

Used for:

- Splash screen
- About page
- Large branding areas
- Application presentation

### Compact Wordmark

Used for:

- Navigation headers
- Player overlays
- Settings
- Secondary headers

### Small/Icon Variant

Used for:

- Xbox application icon
- Launcher
- Small UI elements
- Metadata surfaces

All variants must belong to the same visual identity.

## Logo Assets

Prepare appropriate assets for:

- Xbox application icon
- Splash screen
- Home interface
- Navigation
- Player
- Settings
- About
- Error screens
- Empty states
- Package/store metadata where applicable

Use vector or high-resolution assets when supported.

Never enlarge a low-resolution raster image.

## Splash Screen

Use:

```text
Solid black background
+
Centered Xmana wordmark
+
Subtle short fade animation
```

Keep the splash screen as short as practical.

Do not make the splash screen a fake loading screen.

---

# 3. Full Arabic and English Localization

Xmana must support **Arabic and English completely across the entire application**. This is a first-class product requirement, not an optional translation layer.

## Supported UI Languages

Provide these first-class UI languages:

- Arabic (`ar`)
- English (`en`)

The user must be able to change the application language from Settings without reinstalling the app.

## Full Localization Coverage

Every user-visible string must be localizable, including:

- Home navigation
- Movies
- Series
- Anime
- Search
- Advanced search
- Categories
- Language selectors
- Details pages
- Seasons
- Episodes
- Player controls
- Subtitle controls
- Quality controls
- Playback messages
- Resume prompts
- Skip Intro
- Next Episode
- Settings
- Storage controls
- Cache controls
- History
- Favorites
- Watchlist
- Error messages
- Empty states
- Loading states
- Confirmation dialogs
- About page
- Legal text
- Accessibility labels
- Tooltips and controller hints
- Notifications and transient messages

Do not hard-code user-facing English strings directly inside UI components.

## RTL and LTR

Arabic must use a proper **right-to-left (RTL)** layout. English must use a proper **left-to-right (LTR)** layout.

When Arabic is selected:

- Navigation direction must become RTL.
- Text alignment must adapt appropriately.
- Lists and menus must mirror where this improves usability.
- Navigation hierarchy must follow RTL conventions.
- Search and text-entry interfaces must correctly support Arabic input.
- Dialogs and action groups must use appropriate RTL ordering.
- Player overlays and controls must remain intuitive in RTL.
- Progress and seek controls must remain understandable and consistent.

When English is selected, the interface must return to conventional LTR behavior.

Do not create two separate UIs manually. Build one responsive layout system that supports RTL/LTR correctly.

## Dynamic Direction

The UI direction must change cleanly when the language changes. Avoid requiring an application restart unless a particular platform subsystem makes it unavoidable. If a restart is technically required, persist the selected language and restore it automatically.

## Typography and Arabic Fonts

Choose a UI typeface with excellent Arabic and Latin support. It must:

- Render Arabic correctly.
- Render Arabic ligatures correctly.
- Support Arabic diacritics when present.
- Have clear Latin glyphs.
- Remain readable from a TV viewing distance.
- Maintain consistent visual weight between Arabic and English.

Use an appropriate fallback font strategy when a chosen typeface does not contain a required glyph.

Do not use a font that looks good in English but produces poor Arabic shaping or spacing.

## Content Language

Where the public API exposes language-specific content or localization parameters, use the currently selected application language appropriately.

Prefer the API's Arabic metadata when Arabic is selected and English metadata when English is selected, while preserving original titles where required for proper identification.

Do not locally translate movie, series, anime, actor, or director names when the API already provides an official localized title.

## Mixed-Language Content

The application must safely display Arabic, English, numbers, symbols, and mixed-language titles in the same UI.

Examples include:

- Arabic titles containing English words
- English titles containing Arabic text
- Japanese anime titles
- Roman numerals
- Episode numbers
- URLs and technical strings

Use correct bidirectional text handling rather than manually inserting spacing or direction characters throughout the UI.

## Search

Search must fully support both Arabic and English input.

Support:

- Arabic queries
- English queries

- Mixed Arabic/English queries
- Numbers
- Common punctuation
- Clipboard paste where supported

Do not assume search text is Latin-only.

## Date, Number, and Duration Formatting

Respect the active locale when displaying:

- Dates
- Times
- Durations
- Counts
- Ratings where appropriate

Do not localize technical API values themselves; localize their presentation in the UI.

## Player Localization

The custom player must be fully localized. Every control, hint, error, state, and accessibility label must have Arabic and English text.

Examples of required concepts include:

```text
Play / تشغيل
Pause / إيقاف مؤقت
Resume / متابعة
Start Over / البدء من جديد
Skip Intro / تخطي المقدمة
Next Episode / الحلقة التالية
Previous Episode / الحلقة السابقة
Subtitles / الترجمة
Quality / الجودة
Settings / الإعدادات
Retry / إعادة المحاولة
Exit / خروج
```

These are examples only; implement them through the localization system rather than hard-coding these strings.

## Xbox Controller Hints

Controller guidance must also be localized, including button/action hints and contextual instructions.

## Persistence

Store the selected UI language locally.

The selected language must persist across application restarts.

Do not require an online account for localization preferences.

## Localization Architecture

Create a centralized localization system. All UI text should resolve through localization resources or an equivalent strongly typed localization layer.

Recommended conceptual structure:

```text
LocalizationService
├── English resources
└── Arabic resources
```

Keep translations separate from UI logic.

The architecture should make future languages possible without rewriting screens.

## Quality Requirement

Arabic and English must be treated as **equally complete languages**. There must not be screens that are fully translated in one language while remaining partially or mostly English in the other.

Before release, audit every screen, dialog, player overlay, error state, and settings page in both Arabic and English.

---

# 4. Visual Design Language

The application should use a modern dark cinematic design system.

Suggested characteristics:

- Black / near-black background
- White / near-white primary text
- Neutral gray secondary text
- One restrained original accent color
- Large cinematic artwork
- Subtle gradients
- Soft depth/elevation
- Rounded cards where appropriate
- Smooth transitions
- Clear focus states
- Minimal clutter

Do not reproduce Netflix's exact:

- colors
- typography
- spacing
- UI measurements
- branding
- artwork
- logos

The information architecture may be inspired by modern streaming services, but the final Xmana interface must be visually original.

---

# 5. Core Product Requirements

The application must provide:

- Movies
- TV series
- Anime
- Seasons
- Episodes
- Search
- Advanced search
- Categories
- Category + language browsing
- Featured content
- Latest content
- Newly added content
- Popular content where available
- Content groups
- Related content
- Recommendations
- Video details
- Cast and crew
- Ratings where available
- Trailers where available
- Subtitles where available
- Video quality selection
- Intro skipping where supported
- Custom video player
- Local playback progress
- Local watch history
- Local favorites
- Local watchlist
- Local search history
- Local application settings
- Image caching
- Metadata caching
- Robust error handling
- Xbox controller navigation
- Keyboard support where available
- Mouse support where available
- Fast startup
- Smooth animations
- TV-distance readability

---

# 6. Authentication Model

There is **no login system**.

There is **no account system**.

There is **no user authentication**.

There is **no cloud synchronization of personal state**.

Everything user-specific is local to the Xbox installation.

Do not implement:

- Login
- Registration
- Passwords
- OAuth
- Bearer-token authentication
- Account synchronization
- Private user APIs
- Account history synchronization
- Remote favorites
- Remote subscriptions
- Remote notifications
- Remote continue-watching state

Do not request, store, or expose credentials.

Do not attempt to bypass access controls.

---

# 7. API Source of Truth

The repository contains:

```text
API.md
```

This is the primary reference for the documented API behavior.

API base:

```text
https://cinemana.shabakaty.cc/api/android/
```

The implementation must use the documented public APIs.

Do not invent undocumented parameters.

Do not assume two endpoints with similar names behave identically.

Always preserve observed API behavior documented in `API.md`.

---

# Verified API Implementation Baseline

The following observations were **already verified against the live public service during the API research phase**. Do not ask the user to rediscover these values before implementing the corresponding Xmana features. Treat them as observed behavior, not as an official guarantee. The full evidence and test matrix are in `API.md`.

## Recommendation request body

Endpoint:

```text
POST https://recommend.shabakaty.cc/api/recommendation/recommend
```

Confirmed JSON body:

```json
{
  "MovieId": "<video-id>",
  "MovieName": "<video-title>",
  "ReProcessIfExpired": true
}
```

This was tested successfully with HTTP 200. Xmana should use a typed request model rather than requiring callers to supply arbitrary raw JSON. Do not log or persist temporary/signed URLs returned by this service.

## Rating summary request body

Endpoint:

```text
POST https://rating.shabakaty.cc/api/targets/rating-summary-by-targets-types
```

Confirmed body:

```json
{
  "targets": [
    {
      "targetId": "<id>",
      "targetType": "<Movie|Episode>"
    }
  ]
}
```

Observed frontend mapping:

```text
kind == "2"  -> targetType = "Episode"
otherwise    -> targetType = "Movie"

if rootSeries != "0":
    targetId = rootSeries
else:
    targetId = nb
```

The endpoint returned HTTP 200 in testing. An empty `items` array is a valid no-rating result. The frontend `POST /api/ratings/check-rating` route returned HTTP 405 during live testing and must not be treated as a required public feature.

## Other verified facts

- `transcoddedFiles` exposed 240p, 360p, 480p, 720p, and 1080p labels for tested video `1119122`; availability must still be discovered dynamically per response.
- `videoSeasonNumber/1119119` returned four seasons.
- `videoSeason/1119119` returned 86 episodes.
- `staff/actorID/2766/lang/en/level/0` returned Minami Takayama and associated works.
- `videoComment/id/1119122/limit/10/offset/0` returned an empty list.
- `skippingDurations/1119122` returned `{start: [], end: []}`; the time unit remains unknown.
- `video_thumbnails/video_id/1119122` returned `{videoThumbnails: [], status: 204}`.
- `translationFiles/1119122` returned `arTranslationFile`, `enTranslationFile`, `spTranslationFile`, `arTranslationFilePath`, and `enTranslationFilePath`.
- `checkVideoParentalLevel/1119122` returned `{status: "1"}`; the semantic meaning of `1` remains unverified.
- `thumbnail.shabakaty.cc/api/series/seasonposters` accepted the observed `{RootSeries: <seriesId>}` body pattern but returned HTTP 503 during testing, so it must remain optional.

## Do not ask the user to re-research documented facts

Before requesting API details from the user, read `API.md` and `docs/api-verification-matrix.md`. If a fact is marked **verified**, implement it. If it is marked **uncertain**, use a tolerant model and preserve the raw value rather than blocking unrelated work.

# 8. Public API Scope

Integrate all relevant documented public API functionality.

Do not implement the private/authenticated API section.

Public API functionality includes:

- Video metadata
- Related videos
- Parental-level status
- Video files
- Translations
- Skipping information
- Thumbnails
- Public comments
- Seasons
- Episodes
- Banners
- Groups
- New content
- Latest content
- Latest movies
- Latest series
- Popular content
- Category browsing
- Category + language browsing
- Featured movies
- Search
- Advanced search
- Collections where data exists
- Group pagination
- Staff pages
- Categories
- FAQ
- Recommendations
- Public rating summaries

---

# 9. Video API

Implement:

```text
GET /allVideoInfo/id/{id}
GET /relatedVideos/id/{id}/kind/{kind}/level/{level}
GET /checkVideoParentalLevel/id/{id}
GET /transcoddedFiles/id/{id}
GET /translationFiles/id/{id}
GET /skippingDurations/id/{id}
GET /video_thumbnails/video_id/{id}
GET /videoComment/id/{id}/limit/{limit}/offset/{offset}
GET /videoSeasonNumber/id/{id}
GET /videoSeason/id/{id}
```

Treat `allVideoInfo` as the primary video metadata model.

---

# 10. Video Metadata Model

Support fields observed in `allVideoInfo`, including:

```text
nb
en_title
ar_title
other_title
stars
enTranslationFile
arTranslationFile
fileFile
ar_content
en_content
mDate
year
kind
season
img
imgThumb
imgMediumThumb
imgObjUrl
imgMediumThumbObjUrl
filmRating
seriesRating
episodeNummer
rate
isSpecial
itemDate
duration
imdbUrlRef
rootSeries
useParentImg
spTranslationFile
showComments
episode_flag
trailer
audioStreamNumber
parent_skipping
collectionID
isDeleted
imgThumbObjUrl
ratingImg
arTranslationFilePath
enTranslationFilePath
hasIntroSkipping
categories
videoLikesNumber
videoDisLikesNumber
videoLanguages
videoCommentsNumber
videoViewsNumber
directorsInfo
actorsInfo
writersInfo
castable
```

Do not assume fields are always present or correctly typed.

The API sometimes represents numeric values as strings.

Use tolerant deserialization.

---

# 11. Video Files

Implement:

```text
GET /transcoddedFiles/id/{id}
```

Detect available qualities dynamically.

Observed examples include:

```text
240p
360p
480p
720p
1080p
```

Never assume that all qualities exist for every item.

Never hard-code signed stream URLs.

Never store temporary signed URLs permanently.

Never log actual signed media URLs.

The player should request fresh media information when necessary.

---

# 12. Translation Support

Implement:

```text
GET /translationFiles/id/{id}
```

Support available subtitle languages such as:

- Arabic
- English
- Spanish

Do not show subtitle controls when subtitle data is unavailable.

Subtitle preference is local.

---

# 13. Intro Skipping

Use:

```text
GET /skippingDurations/id/{id}
```

and:

```text
hasIntroSkipping
parent_skipping
```

Show a **Skip Intro** button only when the backend actually provides enough information to perform the action.

Never invent skip timings.

The button must be controller accessible.

---

# 14. Thumbnail Support

Implement:

```text
GET /video_thumbnails/video_id/{id}
```

Handle:

```text
status = 204
```

and empty thumbnail results gracefully.

Never make the UI depend on thumbnail availability.

---

# 15. Parental Status

Implement:

```text
GET /checkVideoParentalLevel/id/{id}
```

Preserve the backend status exactly.

Do not invent undocumented meanings for status values.

If parental information prevents playback, display a clear user-facing state rather than an error dump.

---

# 16. Comments

Implement read-only public comments:

```text
GET /videoComment/id/{id}/limit/{limit}/offset/{offset}
```

The UI may provide a comments section when actual comments exist.

Do not implement authenticated comment creation or deletion.

If the endpoint returns an empty list, display an appropriate empty state.

---

# 17. Seasons and Episodes

Implement:

```text
GET /videoSeasonNumber/id/{id}
GET /videoSeason/id/{id}
```

For series:

```text
Series
→ Seasons
→ Episodes
→ Episode Details
→ Player
```

Use the server-provided:

```text
season
episodeNummer
rootSeries
nb
```

Never reconstruct episode lists by guessing IDs.

---

# 18. Discovery APIs

Integrate:

```text
GET /banner/level/{level}

GET /videoGroups/lang/{lang}/level/{level}

GET /newlyVideosItems/level/{level}/offset/{offset}/

GET /latestVideosItems/level/{level}/offset/{offset}/

GET /latestMovies/level/{level}/itemsPerPage/24/page/{page}

GET /latestSeries/level/{level}/itemsPerPage/24/page/{page}

GET /featuredMovies/level/{level}/itemsPerPage/12/page/0

GET /popularFilms/kind/{period}/level/{level}/page/{page}
```

Do not show a content section when the API returns no items.

Do not create fake placeholder content.

---

# 19. Popular Content

Support documented values:

```text
M
W
D
```

If the endpoint currently returns an empty array, handle it gracefully.

Do not assume popular data exists merely because the endpoint exists.

---

# 20. Home Page

Create a premium streaming-service home page.

Recommended structure:

```text
Hero Banner

Continue Watching

Featured Movies

Featured Series

Latest Movies

Latest Series

Anime

Recommended

Popular

Category Rails

Content Groups
```

Only display a section if useful content exists.

Do not leave large blank spaces when a section is unavailable.

Each horizontal content rail should support:

- D-pad navigation
- A-button selection
- Focus animation
- Lazy loading
- Image caching
- Pagination/load more where applicable

---

# 21. Hero Banner

Hero content should include, when available:

- Background artwork
- Gradient overlay
- Title
- Year
- Rating
- Description
- Categories
- Play
- Details
- Trailer where available

Support controller navigation.

Use smooth transitions, but avoid excessive animation.

---

# 22. Category APIs

Implement:

```text
GET /mainCategories?lang={lang}
GET /subCategories?lang={lang}
GET /categoryLangFreqVideo
```

Build:

```text
Categories
Category Details
Language Filter
Movie/Series Filter
```

Category relationships should use the actual API metadata.

---

# 23. Category Video APIs

Implement:

```text
GET /videosByCategory?categoryID={categoryId}&orderby={orderBy}&videoKind={videoKind}&offset={offset}&level={level}
```

and:

```text
GET /videosByCategoryAndLanguage?category_id={category_id}&language_id={language_id}&videoKind={videoKind}&orderby={orderBy}&level={level}&offset={offset}
```

Support observed sort parameters:

```text
asc
desc
title_asc
title_desc
rating_asc
rating_desc
views_asc
views_desc
stars_asc
stars_desc
r_asc
r_desc
```

Do not assume sorting semantics are identical across endpoints.

---

# 24. Generic Video Listing

Implement:

```text
GET /video/V/2/itemsPerPage/{itemsPerPage}/level/{level}/videoKind/{videoKind}/sortParam/{sortParam}/pageNumber/{page}
```

Support pagination.

Do not assume `offset` and `pageNumber` have identical semantics.

The application should know which pagination model belongs to each API endpoint.

Some `sortParam` values behave differently on this endpoint than on `videosByCategory`.

Preserve the observed backend behavior.

---

# 25. Search

## Simple Search

Implement the documented Base64URL search encoding.

Example:

```text
Conan
→
Q29uYW4
```

Use:

```text
GET /video/V/2/itemsPerPage/{N}/video_title_search/{encodedKeyword}/itemsPerPage/{N}/pageNumber/{page}/level/{level}
```

Search requirements:

- Debouncing
- Search history stored locally
- Recent searches
- Clear search
- Empty state
- Loading state
- Error state
- Controller-friendly text entry

Do not send a request for every character typed.

---

# 26. Advanced Search

Implement:

```text
GET /AdvancedSearch?level={level}&{filters}
```

Supported filters:

```text
videoTitle
staffTitle
page
star
year
category_id
type
sort
```

Supported documented types:

```text
movies
series
```

UI should contain:

- Title
- Staff
- Year range
- Minimum rating
- Category
- Content type
- Sort
- Apply
- Reset

Do not claim unsupported sorting behavior.

---

# 27. Video Groups

Implement:

```text
GET /videoGroups/lang/{lang}/level/{level}

GET /videoListPagination/groupID/{groupId}/level/{level}/itemsPerPage/12/page/{page}
```

Each group becomes a reusable content rail.

Support:

- Group title
- Posters
- Horizontal navigation
- Pagination
- Loading
- Empty states

---

# 28. Collections

Integrate documented collection endpoints where they return real content.

For currently empty collections:

- Keep integration available in the API layer.
- Do not present empty sections in the UI.
- Do not fabricate collection data.

---

# 29. Staff

Implement:

```text
GET /staff/actorID/{staffId}/lang/{lang}/level/{level}
```

Staff pages should support:

- Name
- Role
- Image where available
- Related works
- Controller navigation

Cast/director/writer names on details pages should be selectable when the API provides an ID.

---

# 30. FAQ

Implement:

```text
GET /faqQuestion
```

Create an FAQ page in Settings/About.

---

# 31. Recommendations

Implement the documented recommendation service:

```text
POST https://recommend.shabakaty.cc/api/recommendation/recommend
```

Use it for recommendation sections where useful.

If it fails or returns empty results:

- Fall back to `relatedVideos`
- Hide the section if necessary

Do not let recommendation failure break content details.

---

# 32. Public Ratings

Integrate:

```text
POST https://rating.shabakaty.cc/api/targets/rating-summary-by-targets-types
```

Display a rating only when actual rating data exists.

Never fabricate ratings.

Do not implement authenticated user-rating APIs.

---

# 33. Local-Only Personalization

Because Xmana has no account system, maintain local-only features.

## Continue Watching

Store:

```text
videoId
seriesId
season
episode
position
duration
lastPlayed
```

## Watch History

Store:

```text
videoId
watchedAt
position
duration
completed
```

## Favorites

Store video IDs locally.

## Watchlist

Store video IDs locally.

## Search History

Store recent search terms locally.

## Recently Viewed

Store local browsing history.

## Preferences

Store:

```text
preferred quality
subtitle language
autoplay next episode
resume playback
skip intro
```

Nothing above should require an account.

---

# 34. Local Database

Create a proper local persistence layer.

Recommended architecture:

```text
UI
↓
ViewModel
↓
Repository
↓
Remote API + Local Database + Cache
```

Separate:

```text
Remote DTOs
Domain models
Database entities
UI models
```

Do not bind raw API JSON directly to UI controls.

Implement:

- migrations
- corruption recovery
- async operations
- expiration
- cache invalidation
- safe writes
- bounded cache size

---

# 35. Caching

Cache:

- Home metadata
- Categories
- Groups
- Featured content
- Latest content
- Video metadata
- Search results
- Staff information
- Season/episode metadata
- Images

Use:

```text
Cache first
+
Refresh in background
```

where appropriate.

A returning user should see useful cached content immediately.

Do not cache signed media URLs as permanent resources.

---

# 36. Image System

Build an image-loading service supporting:

- Poster
- Thumbnail
- Medium thumbnail
- Banner
- Staff image

Include:

- Memory cache
- Disk cache
- Cancellation
- Placeholder
- Retry
- Corrupt-image handling
- Prefetching

Do not decode huge images on the UI thread.

---

# 37. Details Page

Create a cinematic details screen.

Include, where available:

- Hero artwork
- Title
- Alternative titles
- Year
- Rating
- Description
- Genres
- Languages
- Duration
- Cast
- Directors
- Writers
- Trailer
- Play
- Resume
- Add to Favorites
- Add to Watchlist
- Related content
- Recommendations

For series:

- Season selector
- Episode list
- Episode progress
- Episode description
- Episode duration
- Next episode
- Previous episode

---

# 38. Anime

Anime is a first-class content category.

Do not create a separate technical subsystem just for anime unless necessary.

Anime must work with:

- Categories
- Search
- Advanced search
- Language filtering
- Series
- Seasons
- Episodes
- Favorites
- Watchlist
- History
- Resume playback
- Related content

---

# 39. Custom Video Player

Build a custom Xmana player UI.

Use the native Xbox-supported media framework underneath, but create a custom transport interface.

The player should feel modern and comfortable rather than exposing only default controls.

---

# 40. Player Layout

Implement:

```text
Video surface
+
Top information overlay
+
Bottom transport controls
+
Seek bar
+
Subtitle control
+
Quality control
+
Episode control
+
Player settings
```

Controls:

- Play/Pause
- Seek backward
- Seek forward
- Seek bar
- Current time
- Remaining time
- Subtitle selection
- Quality selection
- Next episode
- Previous episode
- Episode list
- Trailer mode where relevant
- Retry
- Exit

Controls should disappear automatically after inactivity.

---

# 41. Xbox Controller Player Navigation

Controller-first behavior:

```text
A       Confirm / Play / Pause
B       Back / Close overlay
D-Pad   Move focus
Left    Seek backward
Right   Seek forward
Up      Reveal player controls
Down    Reveal episode/options panel
Menu    Player settings
View    Additional player information
LB/RB   Contextual previous/next actions
```

Adjust specific mappings when platform behavior requires it.

The player must never require a mouse.

---

# 42. Seek

Implement intuitive seeking.

Suggested behavior:

```text
Left / Right
→ ±10 seconds

Repeated input
→ accelerated seeking
```

Display a clear visual seek state.

Do not save progress every frame.

Throttle progress writes.

---

# 43. Resume Playback

When a local progress entry exists, display:

```text
Resume
Start Over
```

Do not display the resume prompt when the saved position is effectively at the end.

Completed content should be treated as completed rather than resumed from a few seconds before the end.

---

# 44. Auto Next Episode

For series:

- Detect near-completion
- Display next episode
- Show countdown
- Allow cancel
- Allow manual next episode
- Respect local autoplay setting

Do not force autoplay.

---

# 45. Player Quality

Use the available qualities returned by:

```text
/transcoddedFiles/id/{id}
```

Do not hard-code available qualities.

Store preferred quality locally.

If the selected quality fails:

1. Detect playback failure.
2. Retry appropriately.
3. Offer another available quality.
4. Preserve the user's position when possible.

Never expose signed stream URLs in UI or logs.

---

# 46. Subtitle Player

Support available subtitle tracks.

Provide:

```text
Off
Arabic
English
Spanish
```

only when actually available.

Remember the preferred subtitle locally.

Subtitle controls must be controller accessible.

---

# 47. Playback Reliability

Handle:

- Loading
- Buffering
- Playing
- Paused
- Seeking
- Ended
- Network failure
- HTTP failure
- Invalid media
- Expired media URL
- Player failure
- Retry
- Quality fallback

The player must not crash because a stream fails.

---

# 48. Network Resilience

Handle:

- DNS errors
- Timeout
- Slow connection
- Temporary server failure
- Empty results
- Malformed responses
- HTTP 4xx
- HTTP 5xx
- Connectivity loss during playback

Use:

- Timeouts
- Cancellation
- Limited retries
- Exponential backoff where appropriate
- Request prioritization

Do not retry forever.

---

# 49. UI Architecture

Use a clean architecture such as:

```text
UI
↓
ViewModels
↓
Repositories
↓
Services
↓
API / Database / Cache
```

Separate responsibilities strictly.

Suggested modules:

```text
Xmana.App
Xmana.Core
Xmana.Api
Xmana.Models
Xmana.Database
Xmana.Repositories
Xmana.Playback
Xmana.Navigation
Xmana.Focus
Xmana.UI
Xmana.Tests
```

---

# 50. UI Sections

Recommended application navigation:

```text
Home
Movies
Series
Anime
Categories
Search
Favorites
Watchlist
History
Settings
```

Keep top-level navigation simple.

The user should be able to reach major areas quickly with the controller.

---

# 51. Navigation

Examples:

```text
Home
→ Content
→ Details
→ Player
```

Series:

```text
Series
→ Series Details
→ Season
→ Episode
→ Player
```

The B button must always have deterministic behavior.

Restore previous focus when returning to a page.

---

# 52. Xbox Focus System

Create a central focus system.

Requirements:

- Visible focus state
- Deterministic navigation
- No focus loss
- Modal focus trapping
- Player overlay focus control
- Search focus management
- Rail navigation
- Vertical navigation
- Focus restoration

The focused item must always be obvious.

Avoid tiny or weak focus indicators.

---

# 53. TV-Distance UI

Design for a living-room TV.

Requirements:

- Large typography
- Large controls
- High contrast
- Clear focus
- Comfortable spacing
- Low interaction complexity

Do not build a desktop UI and simply enlarge it.

---

# 54. Accessibility

Support:

- Readable text
- Strong contrast
- Clear labels
- Semantic control names
- Keyboard alternatives where appropriate
- Controller alternatives for all critical actions
- Reduced-motion option where practical

---

# 55. Settings

Create:

```text
Settings
├── Playback
│   ├── Preferred Quality
│   ├── Subtitle Language
│   ├── Autoplay Next Episode
│   ├── Resume Playback
│   └── Skip Intro
│
├── Interface
│   ├── Language
│   └── Reduce Motion
│
├── Storage
│   ├── Cache Size
│   ├── Clear Image Cache
│   ├── Clear Metadata Cache
│   ├── Clear History
│   └── Reset Local Data
│
└── About
    ├── Version
    ├── API Documentation
    ├── Open Source Information
    └── Legal / Attribution
```

No account settings.

---

# 56. Performance

The application must remain usable on original Xbox One hardware.

Prioritize:

- Virtualized lists
- Lazy loading
- Image caching
- Efficient layouts
- Async I/O
- Incremental rendering
- Limited simultaneous network requests
- Minimal visual effects
- GPU-friendly animations

Never perform expensive work synchronously on the UI thread.

---

# 57. Home Loading Strategy

On startup:

1. Initialize local database.
2. Initialize caches.
3. Load cached Home data.
4. Render immediately.
5. Refresh API data in the background.
6. Update sections progressively.

Do not wait for every remote request before showing the UI.

---

# 58. API Request Strategy

Do not send dozens of simultaneous requests without prioritization.

Use:

```text
Hero
→ Continue Watching
→ Primary content rails
→ Secondary rails
→ Lower-priority sections
```

Use concurrency limits.

Cancel requests when the user navigates away when safe.

---

# 59. Error and Empty States

Every major screen must have:

### Loading State

Skeleton UI.

### Empty State

Useful message + navigation option.

### Offline State

Cached data when available.

### Error State

```text
Unable to load content.
Retry
```

### Player Error

```text
Playback failed.
Retry
Change Quality
Go Back
```

Never display raw stack traces or JSON to the user.

---

# 60. Data Validation

Validate all remote JSON.

Handle:

- Missing fields
- Null fields
- Wrong types
- Empty arrays
- Unexpected values
- Unknown enum values
- HTTP errors

Do not allow malformed server data to crash the application.

---

# 61. Local Playback Progress

Create a dedicated playback-progress service.

Responsibilities:

```text
Start playback
Update position
Throttle persistence
Restore position
Mark completion
Clear completed state
Recover from crashes
```

Persist every few seconds or at meaningful lifecycle events.

Do not write on every media position event.

---

# 62. Lifecycle

Handle:

- Startup
- Suspend
- Resume
- Navigation
- Player activation
- Player deactivation
- App termination
- Controller input
- Network state changes

On resume:

- Restore navigation state
- Refresh stale metadata
- Recover playback when appropriate
- Preserve local progress

---

# 63. Security

Never:

- Embed credentials
- Store passwords
- Store authentication tokens
- Log bearer tokens
- Log signed media URLs
- Log signed image URLs
- Execute downloaded code
- Bypass DRM
- Bypass authentication
- Access private user information
- Circumvent service restrictions

Use HTTPS.

Validate remote URLs.

Keep external navigation controlled.

---

# 64. Logging

Development logging should be structured.

Safe logging example:

```text
Playback stream received: yes
Quality: 720p
Video ID: 1119122
```

Never log the actual signed URL.

Production builds should minimize or disable verbose logging.

---

# 65. C# / API Style

Use strongly typed asynchronous APIs.

Example:

```csharp
Task<VideoDetails?> GetVideoInfoAsync(string id);
Task<IReadOnlyList<VideoItem>> GetRelatedVideosAsync(...);
Task<IReadOnlyList<Season>> GetSeasonsAsync(...);
Task<IReadOnlyList<Episode>> GetEpisodesAsync(...);
```

Use cancellation tokens.

Do not scatter raw HTTP requests throughout the UI.

---

# 66. HTTP Layer

Implement:

- HTTP client abstraction
- Timeout handling
- Cancellation
- Retry policy
- JSON parsing
- HTTP status handling
- Safe logging
- Encoding handling

Do not automatically retry unsafe operations.

Since this application is intentionally public/read-oriented, prioritize GET requests.

---

# 67. Models

Separate:

```text
API DTOs
↓
Domain Models
↓
Database Entities
↓
UI Models
```

Do not use raw DTOs everywhere.

Preserve raw server values where necessary for debugging and future compatibility.

---

# 68. Cache Strategy

Use:

```text
Immediate cached display
+
Background refresh
```

for suitable screens.

Set reasonable TTLs for:

- Categories
- Groups
- Home rails
- Video metadata
- Staff
- Search
- Images
- Seasons
- Episodes

Do not permanently trust stale metadata.

---

# 69. Logo Scaling and Safe Areas

Ensure the Xmana logo works across:

- 720p
- 1080p
- 4K
- 16:9 televisions
- Different overscan environments
- Xbox One family
- Xbox Series family

Never stretch or clip the wordmark.

Keep important logo content inside a safe area.

---

# 70. Animation Guidelines

Use:

- Fade
- Slide
- Focus transitions
- Subtle scale
- Page transitions
- Skeleton shimmer

Avoid:

- Long transitions
- Heavy parallax
- Animations that delay input
- Excessive background motion

Controller response must remain immediate.

---

# 71. Player UX Philosophy

The player should prioritize:

```text
Comfort
Clarity
Control
Speed
```

The user should be able to start watching with almost no interaction.

The controls should disappear when not needed and reappear immediately on controller input.

---

# 72. Episode Experience

For a series episode:

Display:

- Series title
- Season
- Episode number
- Episode title
- Description
- Duration
- Progress
- Previous episode
- Next episode

Allow direct navigation to the episode list without leaving playback.

---

# 73. Favorites / Watchlist / History

All local.

Support:

```text
Add
Remove
Sort
Open
Clear
```

Do not synchronize these features to the server.

---

# 74. Offline-Friendly Behavior

Xmana is not an offline video-downloader.

However, metadata should remain useful when temporarily offline.

When there is no connection:

- Show cached Home data
- Show cached categories
- Show cached metadata
- Show local history
- Show local favorites
- Show local watchlist
- Clearly indicate when remote content cannot be refreshed

Do not pretend that unavailable media can be streamed offline.

---

# 75. Project Structure

Use a maintainable repository:

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

The implementation may add subdirectories beneath these roots as needed.

---

# 76. README

The README must explain:

- What Xmana is
- Supported platforms
- Features
- Architecture
- Installation
- Building
- Xbox deployment
- API dependency
- Local storage
- Player architecture
- Limitations
- Responsible usage
- Licensing
- Attribution

Clearly state:

```text
Xmana is an unofficial client implementation and is not affiliated with
or endorsed by Cinemana, Shabakaty, Microsoft, or Netflix.
```

---

# 77. Testing

Create tests for:

## API

- Valid responses
- Empty responses
- Malformed JSON
- Missing fields
- Timeout
- HTTP errors
- Cancellation

## Search

- Normal searches
- Base64URL encoding
- Special characters
- Empty results

## Navigation

- D-pad movement
- Focus restoration
- B-button behavior
- Modal focus
- Player overlay focus

## Playback

- Play
- Pause
- Resume
- Seek
- Subtitles
- Quality
- Completion
- Next episode
- Network interruption

## Local Storage

- Add
- Remove
- Update
- Migration
- Corruption recovery
- Reset

---

# 78. Xbox Compatibility

The application must be designed around the limitations of the weakest target.

Target:

```text
Xbox One
Xbox One S
Xbox One X
Xbox Series S
Xbox Series X
```

Do not require features unavailable on the original Xbox One.

Do not assume every Xbox supports every codec, resolution, HDR mode, or media feature.

Detect actual player/platform capability before selecting an incompatible media stream.

---

# 79. Development Milestones

Do not generate one giant unstructured implementation.

Build in milestones.

## Milestone 1

Project setup:

- Native Xbox project
- Xmana branding
- Theme
- Navigation
- Controller focus
- Splash screen

## Milestone 2

Networking:

- HTTP layer
- API client
- Models
- JSON handling
- Error handling
- Cache

## Milestone 3

Home:

- Banner
- Rails
- Featured
- Latest
- Groups

## Milestone 4

Discovery:

- Movies
- Series
- Anime
- Categories
- Language filters
- Search
- Advanced search

## Milestone 5

Details:

- Movie details
- Series details
- Seasons
- Episodes
- Cast
- Recommendations

## Milestone 6

Player:

- Native playback engine
- Custom controls
- Seek
- Quality
- Subtitles
- Intro skip
- Resume
- Next episode

## Milestone 7

Local features:

- History
- Favorites
- Watchlist
- Continue watching
- Search history
- Settings

## Milestone 8

Performance:

- Caching
- Image optimization
- Request prioritization
- Memory optimization
- Weak-hardware testing

## Milestone 9

Polish:

- Animations
- Focus states
- Error states
- Accessibility
- Controller UX

## Milestone 10

Release:

- Tests
- Packaging
- Deployment documentation
- Final cleanup
- Production configuration

After every milestone:

1. Build.
2. Run tests.
3. Fix compile errors.
4. Validate navigation.
5. Validate controller input.
6. Check for regressions.
7. Continue only after the milestone is stable.

---

# 80. Do Not Fake Functionality

Never fabricate:

- Ratings
- Subtitles
- Intro timings
- Video qualities
- Episode lists
- Recommendations
- Collection contents
- Playback capabilities
- 4K support
- Backend sorting behavior

When data is unavailable:

```text
Hide
or
Gracefully show unavailable
```

Do not invent.

---

# 81. Do Not Duplicate Backend Semantics

Different endpoints can expose similar parameters with different behavior.

Examples:

- `offset`
- `page`
- `sort`
- `sortParam`
- `videoKind`
- `kind`

Keep endpoint-specific behavior.

Do not create a universal abstraction that changes backend meaning.

Use clean abstractions without hiding important API differences.

---

# 82. Performance Priority

Priority order:

```text
1. Reliability
2. Controller responsiveness
3. Playback stability
4. Startup speed
5. Navigation speed
6. Memory efficiency
7. Image loading
8. Animation polish
9. Visual effects
```

Never sacrifice basic usability for decorative effects.

---

# 83. Final Acceptance Test

A first-time user should be able to:

```text
Launch Xmana
↓
See the Home screen quickly
↓
Browse a content rail
↓
Open a movie or series
↓
Read details
↓
Select an episode if necessary
↓
Press Play
↓
Watch the video
↓
Pause
↓
Seek
↓
Change subtitle
↓
Change quality
↓
Use Skip Intro if available
↓
Exit
↓
Return later
↓
Resume playback
```

without requiring an account.

The user should never need to understand the underlying API.

---

# 84. Final Product Standard

Xmana must feel like a real modern streaming application designed specifically for Xbox.

The final result should combine:

```text
Modern streaming-service UX
+
Original Xmana visual identity
+
Native Xbox interaction
+
Controller-first navigation
+
Cinematic presentation
+
Fast content discovery
+
Reliable playback
+
Local personalization
+
Robust network handling
+
Strong caching
+
Clean architecture
+
Professional error handling
+
Production-quality polish
```

Do not stop at a functional prototype.

Build the application as if it is intended to be used daily on a television from a couch.

The final product must be coherent, responsive, visually polished, maintainable, and technically robust.
