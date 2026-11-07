# Cinemana Public API Reference

Unofficial, developer-oriented documentation of the **public/read-oriented** Cinemana/Shabakaty API surface observed during reverse engineering and live testing.

> **Important:** This document records observed behavior. It is not an official Cinemana/Shabakaty API specification and may change at any time.

**Primary host:** `https://cinemana.shabakaty.cc`  
**Android API base:** `https://cinemana.shabakaty.cc/api/android/`  
**Last live verification:** 2026-09-30  
**Scope:** public/read-oriented APIs only. Authentication, account state, personal history, subscriptions, notifications, telemetry, and account-changing operations are intentionally excluded from the implementation scope.

---

## Table of Contents

- [1. Scope and Research Method](#1-scope-and-research-method)
- [2. Base URL and Conventions](#2-base-url-and-conventions)
- [3. Core Video Metadata](#3-core-video-metadata)
- [4. Related Videos and Video Status](#4-related-videos-and-video-status)
- [5. Media, Qualities, Subtitles, Skipping, and Thumbnails](#5-media-qualities-subtitles-skipping-and-thumbnails)
- [6. Series, Seasons, and Episodes](#6-series-seasons-and-episodes)
- [7. Search](#7-search)
- [8. Discovery and Home Data](#8-discovery-and-home-data)
- [9. Categories and Languages](#9-categories-and-languages)
- [10. Video Listings, Sorting, and Pagination](#10-video-listings-sorting-and-pagination)
- [11. Groups and Collections](#11-groups-and-collections)
- [12. Staff and FAQ](#12-staff-and-faq)
- [13. Public Comments](#13-public-comments)
- [14. External Public Services](#14-external-public-services)
- [15. Response Shapes and Models](#15-response-shapes-and-models)
- [16. Endpoint Semantics Confirmed During Testing](#16-endpoint-semantics-confirmed-during-testing)
- [17. Known Uncertainties and Live Backend Quirks](#17-known-uncertainties-and-live-backend-quirks)
- [18. Current Live Test Matrix](#18-current-live-test-matrix)
- [19. Discovery Inventory — Intentionally Excluded APIs](#19-discovery-inventory--intentionally-excluded-apis)
- [20. Safe Reproduction Notes](#20-safe-reproduction-notes)
- [21. Endpoint Index](#21-endpoint-index)
- [22. Disclaimer](#22-disclaimer)

---

## 1. Scope and Research Method

The API map was built by combining:

1. Static inspection of the Cinemana frontend JavaScript.
2. Extraction of endpoint paths, frontend parameter names, and request construction logic.
3. Live requests against the public service.
4. Comparison of response types and response contents across IDs, pages, kinds, categories, languages, and sort values.
5. Explicit separation of **confirmed behavior**, **observed-but-not-fully-understood behavior**, and **empty/unavailable backend behavior**.

The frontend bundle inspected during the research was:

```text
https://cinemana.shabakaty.cc/main.d454b5f16dee02a7a9d8.js
```

Temporary/signed media URLs, credentials, access tokens, and account-specific values were deliberately not published.

### Important research rule

A route existing in JavaScript does **not** by itself mean that the route is reliable, populated, public in all contexts, or suitable for Xmana. The live-test status below takes precedence over assumptions.

---

## 2. Base URL and Conventions

### Main API base

```text
https://cinemana.shabakaty.cc/api/android/
```

All relative paths below are appended to this base unless an absolute host is shown.

### Common parameters

| Parameter | Observed meaning |
|---|---|
| `id` | Video/resource ID, depending on endpoint |
| `level` | Content/access level supplied by the client |
| `page` | Page index on endpoints that use page-based pagination |
| `pageNumber` | Page index on `video/V/2` and the frontend's title-search route |
| `offset` | Endpoint-specific; it is **not globally equivalent to SQL-style offset** |
| `videoKind` | `1` = movies and `2` = series on tested category/listing endpoints |
| `lang` | Frontend language such as `en` or `ar` |
| `orderby` / `sortParam` | Backend-specific sort selector; semantics differ by endpoint |

### Common response styles

#### Direct list

```json
[
  {}
]
```

Observed for endpoints including `all`-style related/season data and several discovery endpoints.

#### `info` envelope

```json
{
  "info": [
    {}
  ]
}
```

Observed for category listing endpoints.

#### Named object

```json
{
  "status": "1"
}
```

Observed for status-style endpoints.

Clients must tolerate additional fields, missing fields, nulls, and inconsistent primitive types.

---

# 3. Core Video Metadata

## `GET /allVideoInfo/id/{id}`

Returns the central metadata object for a movie, series, or episode.

### Example

```bash
BASE_URL='https://cinemana.shabakaty.cc/api/android'
curl -sS "$BASE_URL/allVideoInfo/id/1119122"
```

### Observed top-level fields

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
Likes
DisLikes
CACHE_SHORT
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

### Confirmed types from the tested response

Most scalar values were returned as strings, including IDs, years, ratings, durations, and counters. Notable exceptions in the tested response:

- `showComments` → boolean
- `hasIntroSkipping` → boolean
- `videoCommentsNumber` → integer
- `CACHE_SHORT` → `null` in the tested response
- `categories` → list
- `actorsInfo` / `directorsInfo` / `writersInfo` → lists
- `videoLanguages` → object

Do not blindly deserialize every number as an integer.

### Selected fields

| Field | Observed type | Meaning/use |
|---|---|---|
| `nb` | string | Video ID |
| `en_title` | string | English title |
| `ar_title` | string | Arabic title |
| `kind` | string | Content-kind field |
| `season` | string | Season when applicable |
| `episodeNummer` | string | Episode number when applicable |
| `rootSeries` | string | Parent series ID when applicable |
| `year` | string | Year value |
| `stars` | string | Rating-like value exposed by this API |
| `duration` | string | Duration value |
| `trailer` | string | Trailer reference |
| `imdbUrlRef` | string | IMDb reference |
| `categories` | list | Category objects |
| `videoLanguages` | object | Language metadata |
| `actorsInfo` | list | Actor/staff objects |
| `directorsInfo` | list | Director/staff objects |
| `writersInfo` | list | Writer/staff objects |
| `hasIntroSkipping` | boolean | Indicates intro-skipping support/flag |
| `parent_skipping` | string | Parent skip-related field |
| `showComments` | boolean | Comment-display flag |
| `videoCommentsNumber` | integer | Comment count |
| `videoViewsNumber` | string | View count field |
| `videoLikesNumber` | string | Like count field |
| `videoDisLikesNumber` | string | Dislike count field |

### Staff object

Observed in `actorsInfo`, `directorsInfo`, and `writersInfo`:

```text
nb
name
role
staff_img
staff_img_thumb
staff_img_medium_thumb
```

### Category object

Observed inside `categories`:

```text
en_title
ar_title
```

### Video language object

Observed top-level structure:

```text
en_title
ar_title
```

Do not assume `videoLanguages` is a list; the tested `allVideoInfo` response exposed it as an object.

---

# 4. Related Videos and Video Status

## `GET /relatedVideos/id/{id}/kind/{kind}/level/{level}`

Returns a direct list of related video objects.

The returned items contain rich metadata and overlap substantially with `allVideoInfo`.

### Test

```bash
curl -sS \
  'https://cinemana.shabakaty.cc/api/android/relatedVideos/id/1119122/kind/1/level/0'
```

Observed result:

- response type: direct list
- count: `11` for the tested request

The exact semantic meaning of every `kind` value was not exhaustively established. Do not infer more than the frontend/live evidence proves.

---

## `GET /checkVideoParentalLevel/id/{id}`

Returns a status object.

Observed:

```json
{
  "status": "1"
}
```

The numeric status meaning was **not** independently decoded. Preserve the raw value.

---

# 5. Media, Qualities, Subtitles, Skipping, and Thumbnails

## `GET /transcoddedFiles/id/{id}`

The endpoint name is spelled `transcoddedFiles` in the observed API.

A live test on video `1119122` exposed these quality labels:

```text
240p
360p
480p
720p
1080p
```

The response also included signed playback URLs. Those URLs are intentionally omitted from this document.

### Client rule

- Discover available qualities from the response for each video.
- Do not assume every video has all five qualities.
- Do not hard-code a media URL.
- Do not store a signed URL as a permanent identifier.
- Do not print signed URLs in application logs.

The exact field names of the signed URL properties were not preserved in the public documentation because publishing those concrete values is unnecessary; Xmana should parse the live response according to the runtime shape and never expose the URLs in logs.

---

## `GET /translationFiles/id/{id}`

Observed response keys:

```text
arTranslationFile
enTranslationFile
spTranslationFile
arTranslationFilePath
enTranslationFilePath
```

Observed example for `1119122`:

```json
{
  "arTranslationFile": "",
  "enTranslationFile": "",
  "spTranslationFile": "",
  "arTranslationFilePath": "defaultImages/loading.gif",
  "enTranslationFilePath": "defaultImages/loading.gif"
}
```

Empty translation fields are valid and should result in hidden/unavailable subtitle controls, not an error.

---

## `GET /skippingDurations/id/{id}`

Observed response shape:

```json
{
  "start": [],
  "end": []
}
```

The tested video contained no populated skip timings.

### Important uncertainty

The unit/semantic mapping of populated `start`/`end` values was **not established** because the tested response was empty. Do not assume seconds, milliseconds, or another unit until a non-empty response is verified.

---

## `GET /video_thumbnails/video_id/{id}`

Observed response:

```json
{
  "videoThumbnails": [],
  "status": 204
}
```

This is a valid no-thumbnail result and should not break the UI.

---

# 6. Series, Seasons, and Episodes

## `GET /videoSeasonNumber/id/{id}`

Returns a direct list of season records.

Tested with series `1119119`:

```text
4 season records
season 1
season 2
season 3
season 4
```

Observed season entries exposed a `season` field; a season-record ID/title should not be assumed when only the season number is present.

---

## `GET /videoSeason/id/{id}`

Returns a direct list of episodes for a series.

Tested with series `1119119`:

```text
86 episodes
```

Observed episode fields included:

```text
nb
en_title
season
episodeNummer
```

Observed first records included episodes from seasons 4, 3, 2, and 1, demonstrating that consumers should use `season` and `episodeNummer` rather than assuming the API list is globally grouped in chronological season order.

---

# 7. Search

## Simple title search

Frontend-discovered route:

```text
GET /video/V/2/itemsPerPage/20/video_title_search/{keyword}/itemsPerPage/12/pageNumber/0/level/{level}
```

The search keyword is encoded as **Base64URL without padding**.

Example:

```text
Conan
→
Q29uYW4
```

Python encoding:

```python
import base64

keyword = "Conan"
encoded = base64.urlsafe_b64encode(keyword.encode()).decode().rstrip("=")
print(encoded)  # Q29uYW4
```

The encoded search was confirmed to return Detective Conan-related results.

Do not send the raw keyword to this route unless it is encoded according to the observed frontend behavior.

---

## `GET /AdvancedSearch?level={level}&{filters}`

Observed filter names from the frontend:

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

### `type`

Confirmed working values:

```text
movies
series
```

### `videoTitle`

Confirmed working.

Test:

```text
videoTitle=Conan
```

Observed first page: 12 results, including:

```text
Detective Conan
Conan the Barbarian
Conan the Destroyer
Lupin III vs. Conan
Lupin III vs. Detective Conan
Detective Conan: Countdown to Heaven
Detective Conan: Quarter of Silence
Detective Conan: The Raven Chaser
Detective Conan: The Eleventh Striker
Detective Conan: Sunflowers of Inferno
Detective Conan: The Time Bombed Skyscraper
```

### `year`

Supports range syntax such as:

```text
year=2000,2005
```

The tested result included the inclusive 2000–2005 range.

### `star`

Confirmed to behave as a rating-related filter during testing. It is not documented here as an exact mathematical operator because the full backend comparison rule was not exhaustively characterized.

### `category_id`

Supports comma-separated category IDs.

Example:

```text
category_id=57,60
```

The observed results were consistent with OR-style category selection.

### `staffTitle`

The filter exists, but direct-name testing with `Minami Takayama` did **not** reliably produce the expected Detective Conan result set. Treat this filter as **backend-supported but not reliable as an exact staff-name lookup**.

### `page`

Tested:

```text
page=0
page=1
```

Each test returned 12 items.

### `sort`

Frontend-supported values were observed, but live tests did not produce reliable distinct ordering. Treat AdvancedSearch sorting as **unverified/unreliable** rather than claiming all sort values work.

---

# 8. Discovery and Home Data

## `GET /banner/level/{level}`

Returns a direct banner list.

Tested at `level=0`:

```text
44 items
```

`level=1` returned the same tested set.

---

## `GET /videoGroups/lang/{lang}/level/{level}`

Returns a wrapper object:

```json
{
  "groups": []
}
```

Tested `lang=en`, `level=0`:

```text
11 groups
```

Observed live group examples at that time included:

| Group ID | Title | Pagination | Content count |
|---:|---|---:|---:|
| 404 | Emmy Winners 2026 | 1 | 12 |
| 26 | Featured Movies | 1 | 12 |
| 2 | Featured Series | 1 | 12 |
| 290 | 4K Movie | 1 | 12 |
| 407 | Kara no Kyoukai Movies | 1 | 10 |
| 311 | 4K most popular series | 1 | 12 |
| 392 | 4K Mission: Impossible | 1 | 8 |
| 30 | Marvel Universe 4K | 1 | 12 |
| 386 | 4K James Bond 007 | 1 | 12 |
| 377 | Latest Asian Series | 1 | 12 |
| 4 | Latest Series | 1 | 12 |

These titles are **live content examples, not permanent API constants**.

---

## `GET /newlyVideosItems/level/{level}/offset/{offset}/`

Frontend normally uses `offset=12`, but direct tests accepted other values.

Observed behavior:

```text
offset=0  → []
offset=1  → 1 result
offset=2  → 2 results
offset=11 → 11 results
offset=12 → 12 results
offset=13 → 12 results
offset=24 → 12 results
offset=36 → 12 results
```

The result set stayed anchored at the newest content rather than behaving like a conventional skip offset.

**Conclusion:** here `offset` behaves like a requested count/limit with backend-side capping, not like a conventional skip offset.

---

## `GET /latestVideosItems/level/{level}/offset/{offset}/`

Observed behavior at `level=0`:

```text
offset=0  → 0
offset=1 → 1
offset=2 → 2
offset=11 → 11
offset=12 → 12
offset=13 → 13
offset=24 → 24
```

(The `s ` prefix above is only a documentation typo guard; the actual parameter value is numeric `1`, `2`, `11`, etc.)

This endpoint also behaves like a count/limit rather than a standard skip offset.

---

## `GET /latestMovies/level/{level}/itemsPerPage/24/page/{page}/`

Returns latest movies.

Tested:

- page 0 → 24 items
- page 1 → 24 items

---

## `GET /latestSeries/level/{level}/itemsPerPage/24/page/{page}/`

Returns latest series.

Tested:

- page 0 → 24 items
- page 1 → 24 items

---

## `GET /featuredMovies/level/{level}/itemsPerPage/12/page/0`

Returns featured movies.

Tested:

- 12 items
- `level=0` and `level=1` produced the same tested 12-item set

---

## `GET /popularFilms/kind/{period}/level/{level}/page/{page}`

Frontend period values:

```text
M
W
D
```

Live tests for pages 0 and 1 returned `[]` for all three values.

**Conclusion:** endpoint exists and is used by the frontend, but the tested backend currently returned no records for supported frontend periods.

---

# 9. Categories and Languages

## `GET /mainCategories?lang={lang}`

Observed main categories and IDs:

| ID | English title |
|---:|---|
| 84 | Action |
| 56 | Adventure |
| 57 | Animation |
| 59 | Comedy |
| 60 | Crime |
| 61 | Documentary |
| 62 | Drama |
| 67 | Fantasy |
| 70 | Horror |
| 76 | Mystery |
| 77 | Romance |
| 78 | Sci-Fi |
| 79 | Sport |
| 80 | Thriller |
| 89 | Western |

---

## `GET /subCategories?lang={lang}`

Observed entries included:

| ID | English title |
|---:|---|
| 58 | Biography |
| 65 | Family |
| 68 | History |
| 75 | Musical |
| 81 | War |
| 87 | Supernatural |
| 88 | Music |
| 90 | Talk-Show |
| 97 | Short |
| 101 | Reality-TV |
| 102 | Arabic dubbed |
| 104 | News |
| 105 | Ecchi |
| 106 | Film-Noir |
| 111 | Game |
| 112 | Psychological |
| 113 | Slice of Life |
| 118 | Game-Show |
| 123 | Magic |
| 124 | Super Power |
| 125 | Seinen |
| 126 | Shounen |
| 127 | School |
| 128 | Sports |
| 130 | Iraqi |
| 149 | Program |
| 152 | Religious |
| 155 | Biopic series |
| 158 | Dialogue |
| 161 | Entertaining |
| 164 | Bollywood |
| 167 | War & Politics |
| 170 | Reality |
| 173 | Soap |

The exact returned order is not a contract.

---

## `GET /categoryLangFreqVideo`

Returns category/language frequency metadata.

Observed fields include:

```text
nb
ar_title
en_title
langArray
```

Nested language records observed inside `langArray` include:

```text
catNb
langFreq
lang_ar_title
lang_en_title
langNb
langFreqSeries
```

For Action category `84`, tested language entries included:

| Language ID | Observed language |
|---:|---|
| 7 | English |
| 9 | Arabic |
| 10 | Hindi |
| 11 | French |
| 12 | German |
| 13 | Italian |
| 14 | Spanish |
| 21 | Chinese |
| 22 | Japanese |
| 23 | Korean |
| 24 | Russian |
| 25 | Turkish |
| 26 | Norwegian |
| 27 | Persian |
| 35 | Swedish |
| 36 | Hungary |

These are observed mappings from a live response and can change with backend metadata.

---

## `GET /videosByCategory?categoryID={categoryId}&orderby={orderBy}&videoKind={videoKind}&offset={offset}&level={level}`

Response shape:

```json
{
  "info": [
    {}
  ]
}
```

Observed `videoKind` values:

```text
1 = Movies
2 = Series
```

For tested category `57` (Animation), both movie and series modes were populated.

Observed sort values that changed ordering/result selection:

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

Tested responses commonly contained 30 items.

Important: the meaning of `r_asc` / `r_desc` was not given a human-readable backend field name; document it as an observed sort mode rather than guessing its exact database field.

---

## `GET /videosByCategoryAndLanguage?category_id={category_id}&language_id={language_id}&videoKind={videoKind}&orderby={orderBy}&level={level}&offset={offset}`

Response shape:

```json
{
  "info": [
    {}
  ]
}
```

Test example:

```text
category_id=57
language_id=22
videoKind=1
orderby=desc
offset=0
level=0
```

This returned Japanese-language Animation movies.

The corresponding `videoKind=2` request returned Japanese-language Animation series.

The same ordering family was tested successfully here:

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

`offset` behaved like a result-range/skip parameter here, unlike `newlyVideosItems` and `latestVideosItems`, so Xmana must not use one universal offset abstraction.

---

# 10. Video Listings, Sorting, and Pagination

## `GET /video/V/2/itemsPerPage/{itemsPerPage}/level/{level}/videoKind/{videoKind}/sortParam/{sortParam}/pageNumber/{page}`

General paginated video listing endpoint.

Observed pagination:

```text
itemsPerPage=12, page=0 → 12
itemsPerPage=12, page=1 → 12
itemsPerPage=30        → 30
itemsPerPage=60        → 60
```

Observed first-page behavior:

```text
videoKind=1, sortParam=desc → current/latest movie set
videoKind=2, sortParam=desc → current/latest series set
```

### Sort findings

| `sortParam` | Observed result |
|---|---|
| `asc` / `desc` | Clearly changes result set/order |
| `stars_asc` / `stars_desc` | Clearly changes result set/order; `stars` is visible in returned objects |
| `r_asc` / `r_desc` | Clearly changes result set/order; exact backing field not established |
| `rating_asc` / `rating_desc` | Produces different result sets; exact backing field not established |
| `views_asc` / `views_desc` | Produces different result sets; returned objects did not expose a `views` field in the tested response |
| `title_asc` / `title_desc` | Same first-page result set/order in the tested run; not confirmed as alphabetical sorting |

Do not silently translate these values into a guessed database field mapping.

---

# 11. Groups and Collections

## `GET /videoGroups/lang/{lang}/level/{level}`

Response:

```json
{
  "groups": [
    {}
  ]
}
```

Observed group fields:

```text
groupsID
title
hasPagination
content
```

See the live examples in [Section 8](#8-discovery-and-home-data).

---

## `GET /videoListPagination/groupID/{groupId}/level/{level}/itemsPerPage/12/page/{page}`

Paginated group-content endpoint.

Tested with `groupID=404`:

- page 0 → 12 items
- page 1 → 2 items

This confirms real page-based pagination for this endpoint.

---

## `GET /collectionsId/`

Direct collection list endpoint.

Tested response:

```json
[]
```

**Status:** reachable but returned no collection IDs during testing.

---

## `GET /collectionVideos/collectionID/{id}/level/{level}`

Collection content endpoint discovered from frontend code.

Tested collection IDs (including IDs associated with frontend collection/group flows) returned empty lists.

**Status:** endpoint is known, but populated response behavior remains unverified.

---

# 12. Staff and FAQ

## `GET /staff/actorID/{staffId}/lang/{lang}/level/{level}`

Public staff/work endpoint.

Tested with:

```text
staffId=2766
```

which corresponded to **Minami Takayama** in the tested metadata.

Observed result included:

- staff title/name
- role
- associated title list
- localized title/role behavior when changing `lang`

The tested English result contained 24 associated items.

Observed staff object fields:

```text
nb
name
role
staff_img
staff_img_thumb
staff_img_medium_thumb
```

Do not assume the full response is only the staff object; the endpoint also returns associated work data.

---

## `GET /faqQuestion`

Public FAQ endpoint.

Confirmed reachable and returned FAQ data in live testing.

The exact question/answer field schema was not preserved in the initial extraction and should therefore be handled using tolerant JSON models until a concrete populated response schema is intentionally sampled.

---

# 13. Public Comments

## `GET /videoComment/id/{id}/limit/{limit}/offset/{offset}`

This endpoint is also listed under video information because it returns a video-specific list.

The tested request:

```text
id=1119122
limit=10
offset=0
```

returned:

```json
[]
```

A populated comment-object schema was therefore not confirmed.

Xmana should expose the section only when non-empty data is received and otherwise show an empty state.

Authenticated comment-write endpoints are excluded from Xmana's public-only scope.

---

# 14. External Public Services

## Recommendations

Endpoint:

```text
POST https://recommend.shabakaty.cc/api/recommendation/recommend
```

### Confirmed request body

```json
{
  "MovieId": "<video-id>",
  "MovieName": "<video-title>",
  "ReProcessIfExpired": true
}
```

This exact body was tested successfully against the live endpoint and returned HTTP `200` with recommendation data.

A test used movie ID `3143018`.

### Response notes

The observed response contained recommendation results and expiration-related information, including an `expirationDate` and a temporary/refresh-style indicator. Individual response items may contain temporary/signed URLs.

Do not publish or log those URLs.

---

## Ratings

Endpoint:

```text
POST https://rating.shabakaty.cc/api/targets/rating-summary-by-targets-types
```

### Confirmed request body

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

### Confirmed target-type mapping from frontend logic

```text
kind == "2"  →  "Episode"
otherwise    →  "Movie"
```

### Confirmed target-ID selection logic

```text
if rootSeries != "0":
    targetId = rootSeries
else:
    targetId = nb
```

This means an episode belonging to a series uses the series/root-series ID with `Episode` as the target type, while a standalone movie uses its own `nb` and `Movie`.

### Observed response

The endpoint returned HTTP `200` during testing. One tested case returned:

```json
{
  "items": [],
  "totalCount": 0
}
```

An empty summary means there was no rating summary returned for that target; Xmana must not fabricate a rating.

### Frontend/backend mismatch

The frontend also references:

```text
POST /api/ratings/check-rating
```

with target-related fields, but the live backend returned HTTP `405 Method Not Allowed` during testing and advertised allowed methods including `DELETE,GET,OPTIONS`.

**Do not make `check-rating` a required public feature.**

---

## Series season-poster service

Endpoint discovered in the frontend:

```text
POST https://thumbnail.shabakaty.cc/api/series/seasonposters
```

Observed request body pattern:

```json
{
  "RootSeries": "<series-id>"
}
```

A live test returned HTTP `503` with an application-unavailable response.

**Status:** discovered, but currently unavailable/unreliable. Xmana must not depend on this service for basic series navigation.

---

# 15. Response Shapes and Models

## Video object

A typical video object across public listing endpoints may contain a subset of:

```text
nb
en_title
ar_title
other_title
stars
year
kind
season
episodeNummer
rootSeries
img
imgThumb
imgMediumThumb
imgObjUrl
imgMediumThumbObjUrl
filmRating
seriesRating
rate
isSpecial
itemDate
duration
imdbUrlRef
showComments
trailer
collectionID
isDeleted
categories
videoLanguages
```

Do not require every field on every endpoint.

## Direct list vs `info` envelope

Known direct-list endpoints include tested responses from:

```text
relatedVideos
videoSeasonNumber
videoSeason
banner
latestMovies
latestSeries
featuredMovies
videoComment
video/V/2
```

Known `info`-envelope endpoints include:

```text
videosByCategory
videosByCategoryAndLanguage
```

Known named-wrapper endpoints include:

```text
videoGroups → { groups: [...] }
checkVideoParentalLevel → { status: ... }
video_thumbnails → { videoThumbnails: [...], status: ... }
translationFiles → named translation fields
skippingDurations → { start: [...], end: [...] }
```

---

# 16. Endpoint Semantics Confirmed During Testing

This section is deliberately concise: it records the behaviors that are most likely to be incorrectly re-invented by an implementation agent.

## `videoKind`

On tested category/listing endpoints:

```text
1 = Movies
2 = Series
```

## `kind` for ratings

Frontend logic used:

```text
kind == "2" → Episode
otherwise   → Movie
```

This mapping is specifically confirmed for the rating-summary target type logic, not claimed as a universal meaning for every API endpoint.

## `offset` is endpoint-specific

Do **not** create one generic `offset` abstraction with universal semantics.

Observed examples:

```text
newlyVideosItems → acts like requested result count / capped limit
latestVideosItems → acts like requested result count
videosByCategory → backend range/offset behavior
videosByCategoryAndLanguage → backend range/offset behavior
```

## Pagination

The following were positively confirmed as page-based:

```text
latestMovies
latestSeries
videoListPagination
video/V/2
AdvancedSearch page parameter
```

## `video/V/2` item count

`itemsPerPage` was respected for 12, 30, and 60 in testing.

## Search encoding

Simple title search uses Base64URL without padding.

## Advanced search title filtering

`videoTitle` is confirmed to work.

## Advanced search sort

Do not rely on `sort` as a trustworthy ordering mechanism.

## Category sorting

The category endpoints demonstrated the documented sorting family working in live tests.

## Generic listing sorting

Some generic-listing sort values work, but title sorting was not reliable in the tested backend.

---

# 17. Known Uncertainties and Live Backend Quirks

The following are intentionally not guessed:

1. The precise semantic meaning of the numeric `status` returned by `checkVideoParentalLevel`.
2. The complete meaning of all `kind` values for `relatedVideos`.
3. The exact field names inside `transcoddedFiles` for signed URLs.
4. The unit and semantics of populated `skippingDurations.start/end` values because the test response contained no ranges.
5. The exact schema of a populated comment object because the tested video had zero comments.
6. The exact FAQ field schema because the first verification focused on reachability rather than schema capture.
7. The complete response schema of the recommendation service because response items can contain sensitive/temporary URLs that were intentionally not copied into the documentation.
8. Exact backend meaning of `r_asc` / `r_desc`.
9. Reliable alphabetic behavior for `video/V/2` `title_asc/title_desc`.
10. Reliable exact-name behavior for `AdvancedSearch.staffTitle`.
11. `AdvancedSearch.sort` semantics.
12. Why `popularFilms` is currently empty for the frontend's supported periods.
13. Why collection endpoints are currently empty.
14. The `thumbnail.shabakaty.cc` season-poster service was unavailable (`503`) during testing.
15. API behavior may change with content, deployment, locale, client version, or time.

### Implementation rule

Unknown does **not** mean unsupported.

When implementing an uncertain response:

- deserialize known fields when present;
- keep unknown JSON available when practical;
- do not crash on missing fields;
- do not invent semantic labels;
- do not block unrelated app features on optional services.

---

# 18. Current Live Test Matrix

## Core sample IDs

| Resource | ID | Purpose |
|---|---:|---|
| Series | `1119119` | Season/episode testing |
| Episode | `1119122` | Video metadata/media testing; series S1E2 |
| Detective Conan movie | `192319` | Staff/cast testing |
| Staff: Minami Takayama | `2766` | Staff endpoint testing |
| Animation category | `57` | Category testing |
| Crime category | `60` | Multi-category testing |
| Action category | `84` | Category-language frequency testing |
| Japanese language | `22` | Category + language testing |
| Emmy Winners group | `404` | Group pagination testing |

## Series `1119119`

Observed:

```text
4 seasons
86 episodes
```

## Episode `1119122`

Observed through public endpoints:

```text
allVideoInfo → full video object
relatedVideos → 11 related items in tested request
transcoddedFiles → 240p/360p/480p/720p/1080p labels
translationFiles → translation metadata object
skippingDurations → empty ranges
video_thumbnails → status 204 / no thumbnails
videoComment → empty list for tested request
checkVideoParentalLevel → status "1"
```

## Advanced search `videoTitle=Conan`

Observed:

```text
12 first-page results
```

and Conan/Detective Conan titles were returned, confirming title filtering.

## Category + language

Tested:

```text
category_id=57
language_id=22
videoKind=1/2
```

and received Japanese Animation movies and series.

## Generic listing

Tested `itemsPerPage`:

```text
12
30
60
```

and the corresponding result counts matched.

---


## Public-candidate endpoint: `POST /getVideosByIds`

This route was identified during frontend API extraction. The frontend constructs it as a form-urlencoded POST with an `ids` field.

Observed request encoding:

```text
Content-Type: application/x-www-form-urlencoded
ids=<video-id-list>
```

The exact live response schema was not captured during the original public verification pass. Treat this endpoint as **discovered but not live-schema-verified**. Xmana should not make a core feature depend on it until a representative response is captured.

# 19. Discovery Inventory — Intentionally Excluded APIs

Static frontend analysis found additional APIs related to accounts, personalized state, settings changes, analytics, and authenticated workflows. They are recorded here so implementation agents know that they were **deliberately excluded**, not accidentally forgotten.

## User/account state

```text
POST /history
POST /addToHistory
POST /removeFromHistory
POST /get_subscriptions
POST /add_subscriptions
POST /remove_subscriptions
POST /reportIssue
POST /get_subscription_status
GET  /continueWatching/level/{level}/itemPerPage/{pageSize}/page/{page}
POST /get_notifications
POST /resetNewNotifications
POST /addComment
POST /removeComment
```

Observed body fields from frontend construction included items such as:

```text
history → kind, level, pageNumber, sortParam
addToHistory → kind, videoId
removeFromHistory → kind, videoId
subscriptions → sortParam, level
add/remove subscriptions → video_id
reportIssue → videoId, issueId
get_subscription_status → videoId
addComment → comment, videoId
removeComment → commentId, videoId
```

These were **not included in Xmana's public-only implementation scope**.

## Settings / account services

```text
GET  /userSettings/lang/{lang}
POST /changeParentalLevel
POST /UserTranslationSettings
GET  https://account.shabakaty.cc/core/api/device?userCode={code}
```

These were excluded because they are user/account/device-state functionality rather than the public content catalog.

## Account/OAuth family discovered in frontend code

The frontend references account/identity routes under:

```text
https://account.shabakaty.cc/core/api/account/
https://account.shabakaty.cc/core/api/account/picture
https://account.shabakaty.cc/core/api/password/client-forgot/
https://account.shabakaty.cc/core/api/password/reset/
https://account.shabakaty.cc/core/api/registration/
https://account.shabakaty.cc/core/connect/facebook
https://account.shabakaty.cc/core/connect/google
https://account.shabakaty.cc/core/connect/token/
https://account.shabakaty.cc/core/connect/userinfo/
```

No credentials, client secrets, passwords, access tokens, refresh tokens, or social-login material is reproduced here.

## Continue-watching service

Frontend references were also observed under:

```text
https://contwatch.shabakaty.cc/api/progress/set
https://contwatch.shabakaty.cc/api/progress/media-progress
https://contwatch.shabakaty.cc/api/progress/finished-list
https://contwatch.shabakaty.cc/api/progress/unfinished-list
```

These are intentionally not used by Xmana's local-only model.

## Analytics

Frontend references included:

```text
POST /api/info/userInfo/
POST /api/info/ShabakatyInfo/
```

These are telemetry/analytics operations and are intentionally excluded. Xmana should not reproduce the site's telemetry pipeline just to provide public content browsing.

## Account rating check mismatch

```text
POST /api/ratings/check-rating
```

was discovered in frontend code but returned `405` from the live backend. It is therefore not part of the public implementation contract.

---

# 20. Safe Reproduction Notes

## Minimal read-only tests

```bash
BASE_URL='https://cinemana.shabakaty.cc/api/android'

curl -sS "$BASE_URL/allVideoInfo/id/1119122"
curl -sS "$BASE_URL/videoSeason/id/1119119"
curl -sS "$BASE_URL/mainCategories?lang=en"
curl -sS "$BASE_URL/subCategories?lang=en"
curl -sS "$BASE_URL/categoryLangFreqVideo"
curl -sS "$BASE_URL/faqQuestion"
curl -sS "$BASE_URL/banner/level/0"
curl -sS "$BASE_URL/latestMovies/level/0/itemsPerPage/24/page/0/"
curl -sS "$BASE_URL/latestSeries/level/0/itemsPerPage/24/page/0/"
```

### Advanced search example

```bash
curl -sS \
  'https://cinemana.shabakaty.cc/api/android/AdvancedSearch?level=0&videoTitle=Conan&page=0'
```

### Simple search encoding example

```bash
python - <<'PY'
import base64
q = "Conan"
print(base64.urlsafe_b64encode(q.encode()).decode().rstrip("="))
PY
```

### Recommendation example

```bash
curl -sS -X POST \
  'https://recommend.shabakaty.cc/api/recommendation/recommend' \
  -H 'Content-Type: application/json' \
  --data '{
    "MovieId":"3143018",
    "MovieName":"<known-title>",
    "ReProcessIfExpired":true
  }'
```

### Rating summary example

```bash
curl -sS -X POST \
  'https://rating.shabakaty.cc/api/targets/rating-summary-by-targets-types' \
  -H 'Content-Type: application/json' \
  --data '{
    "targets":[
      {
        "targetId":"192319",
        "targetType":"Movie"
      }
    ]
  }'
```

Only use public/read-oriented requests from the examples above.

---

# 21. Endpoint Index

## Main Cinemana API

```text
GET  /allVideoInfo/id/{id}
GET  /relatedVideos/id/{id}/kind/{kind}/level/{level}
GET  /checkVideoParentalLevel/id/{id}
GET  /transcoddedFiles/id/{id}
GET  /translationFiles/id/{id}
GET  /skippingDurations/id/{id}
GET  /video_thumbnails/video_id/{id}
GET  /videoComment/id/{id}/limit/{limit}/offset/{offset}
GET  /videoSeasonNumber/id/{id}
GET  /videoSeason/id/{id}

GET  /banner/level/{level}
GET  /videoGroups/lang/{lang}/level/{level}
GET  /newlyVideosItems/level/{level}/offset/{offset}/
GET  /latestVideosItems/level/{level}/offset/{offset}/
GET  /latestMovies/level/{level}/itemsPerPage/24/page/{page}/
GET  /latestSeries/level/{level}/itemsPerPage/24/page/{page}/
GET  /popularFilms/kind/{period}/level/{level}/page/{page}
GET  /featuredMovies/level/{level}/itemsPerPage/12/page/0

GET  /mainCategories?lang={lang}
GET  /subCategories?lang={lang}
GET  /categoryLangFreqVideo
GET  /videosByCategory?categoryID={categoryId}&orderby={orderBy}&videoKind={videoKind}&offset={offset}&level={level}
GET  /videosByCategoryAndLanguage?category_id={category_id}&language_id={language_id}&videoKind={videoKind}&orderby={orderBy}&level={level}&offset={offset}

GET  /video/V/2/itemsPerPage/{itemsPerPage}/level/{level}/videoKind/{videoKind}/sortParam/{sortParam}/pageNumber/{page}
GET  /video/V/2/itemsPerPage/20/video_title_search/{keyword}/itemsPerPage/12/pageNumber/0/level/{level}
GET  /AdvancedSearch?level={level}&{filters}

GET  /videoListPagination/groupID/{groupId}/level/{level}/itemsPerPage/12/page/{page}
GET  /collectionsId/
GET  /collectionVideos/collectionID/{id}/level/{level}
GET  /staff/actorID/{staffId}/lang/{lang}/level/{level}
GET  /faqQuestion
POST /getVideosByIds  # discovered; response schema not live-verified
```

## External public services

```text
POST https://recommend.shabakaty.cc/api/recommendation/recommend
POST https://rating.shabakaty.cc/api/targets/rating-summary-by-targets-types
POST https://thumbnail.shabakaty.cc/api/series/seasonposters
```

`thumbnail.shabakaty.cc` was unavailable during the live test (`503`).

---

# 22. Disclaimer

This documentation is an independent technical research reference based on publicly observable service behavior and client-side implementation details.

It is **not** an official Cinemana/Shabakaty API specification, does not guarantee future compatibility, and does not claim that every observed endpoint is intended for third-party applications.

Xmana should use only the documented public/read-oriented surface described in this file, must not expose or harvest credentials, and must not bypass authentication, DRM, service restrictions, or access controls.
