# Cinemana Public API Map

> Reverse-engineered public API reference for `cinemana.shabakaty.cc`.
>
> Tested against the live service on **September 30, 2026**. This document records observed behavior from direct requests and frontend JavaScript analysis. It does **not** claim that every undocumented field or endpoint is stable.

## Scope

This document covers the **public/read-oriented API surface** that was identified and tested without documenting account-login flows.

The following were intentionally excluded from this reference:

- Login, OAuth, and token flows
- User profile/account operations
- History, watch-later, favorites, and subscription state
- Notifications and user settings
- Continue-watching operations tied to a user account
- Analytics/telemetry submission
- Account-changing POST operations
- Credentials, client secrets, access tokens, refresh tokens, or other authentication material
- Temporary/signed media URLs

## Base URL

```text
https://cinemana.shabakaty.cc/api/android/
```

---

## 1. Video Information

### `GET allVideoInfo/id/{id}`

Returns the main metadata object for a video/movie/episode.

Observed top-level fields include:

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

`categories` is a list of objects containing fields such as:

```text
en_title
ar_title
```

`actorsInfo`, `directorsInfo`, and `writersInfo` contain staff objects with fields such as:

```text
nb
name
role
staff_img
staff_img_thumb
staff_img_medium_thumb
```

### `GET relatedVideos/id/{id}/kind/{kind}/level/{level}`

Returns a list of related videos. The returned objects contain substantial video metadata rather than only IDs.

Observed fields overlap heavily with `allVideoInfo`, including title, year, rating, media metadata, categories, and poster information.

The exact semantic meaning of every `kind` value was not exhaustively established.

### `GET checkVideoParentalLevel/id/{id}`

Returns a status object.

Observed response:

```json
{
  "status": "1"
}
```

The numeric status value is recorded as observed; its exact meaning was not independently established.

### `GET videoComment/id/{id}/limit/{limit}/offset/{offset}`

Returns comments for a video.

For the tested video, the response was:

```json
[]
```

The endpoint is public/readable, but no populated comment sample was available during testing.

---

## 2. Media, Subtitles, Skipping, and Thumbnails

### `GET transcoddedFiles/id/{id}`

Returns available transcoded video qualities.

A tested video exposed five quality levels:

```text
240p
360p
480p
720p
1080p
```

Temporary/signed media URLs are intentionally not reproduced here.

### `GET translationFiles/id/{id}`

Returns translation-file metadata.

Observed keys:

```text
arTranslationFile
enTranslationFile
spTranslationFile
arTranslationFilePath
enTranslationFilePath
```

### `GET skippingDurations/id/{id}`

Returns intro/skip timing data.

Observed response for the tested video:

```json
{
  "start": [],
  "end": []
}
```

### `GET video_thumbnails/video_id/{id}`

Returns video thumbnail metadata.

Observed response for the tested video:

```json
{
  "videoThumbnails": [],
  "status": 204
}
```

A `204` status was returned inside the JSON object for the tested video, indicating no thumbnail list was available there.

---

## 3. Series, Seasons, and Episodes

### `GET videoSeasonNumber/id/{id}`

Returns the seasons associated with a series.

For series `1119119`, the endpoint returned four season entries:

```text
1
2
3
4
```

The response is a list.

### `GET videoSeason/id/{id}`

Returns the episode list associated with a series.

For series `1119119`, the endpoint returned **86 episodes** across four seasons.

Observed episode fields include:

```text
nb
en_title
season
episodeNummer
```

---

## 4. Search

### Simple title search

```text
GET video/V/2/itemsPerPage/{itemsPerPage}/video_title_search/{keyword}/itemsPerPage/{N}/pageNumber/{page}/level/{level}
```

The search keyword is encoded using **Base64URL without padding**.

Example:

```text
Conan
->
Q29uYW4
```

The encoded query was confirmed to return Detective Conan results.

### `GET AdvancedSearch?level={level}&{filters}`

Observed filter names:

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

#### `type`

Observed working values:

```text
movies
series
```

#### `videoTitle`

Confirmed to work.

Example:

```text
videoTitle=Conan
```

returned 12 first-page results including:

```text
Detective Conan
Conan the Barbarian
Conan the Destroyer
Lupin III vs. Conan
Lupin III vs. Detective Conan
```

#### `year`

Supports a range such as:

```text
year=2000,2005
```

The tested result set covered the inclusive year range.

#### `star`

Confirmed to act as a rating-related filter in testing.

#### `category_id`

Supports multiple category IDs, for example:

```text
category_id=57,60
```

The observed behavior was consistent with an OR-style category selection.

#### `staffTitle`

The parameter exists, but direct staff-name searching did not reliably return the expected staff-associated titles during testing. Treat this filter as **unreliable/unverified** for exact staff lookup.

#### `sort`

Multiple sort values were tested, but `AdvancedSearch` did not consistently produce distinct ordering. The sort parameter should therefore be considered **unreliable in this endpoint**.

---

## 5. Discovery and Home-Page Data

### `GET banner/level/{level}`

Returns the banner list.

A test at `level=0` returned 44 items.

### `GET latestMovies/level/{level}/itemsPerPage/24/page/{page}`

Returns paginated latest movies.

The tested endpoint returned 24 items per page.

### `GET latestSeries/level/{level}/itemsPerPage/24/page/{page}`

Returns paginated latest series.

The tested endpoint returned 24 items per page.

### `GET featuredMovies/level/{level}/itemsPerPage/12/page/0`

Returns featured movies.

The tested endpoint returned 12 items.

### `GET newlyVideosItems/level/{level}/offset/{offset}/`

Observed behavior suggests `offset` functions more like a requested result count than a conventional skip offset.

Examples from testing:

```text
offset=1  -> 1 result
offset=12 -> 12 results
offset=24 -> 12 results
```

The first results remained anchored to the same newest items rather than shifting by `offset`.

### `GET latestVideosItems/level/{level}/offset/{offset}/`

Similarly, the tested endpoint behaved as a count/limit-like parameter.

Examples:

```text
offset=1  -> 1 result
offset=12 -> 12 results
offset=24 -> 24 results
```

### `GET popularFilms/kind/{period}/level/{level}/page/{page}`

Frontend-discovered period values:

```text
M
W
D
```

The live backend returned an empty list for all tested combinations of these periods and pages. The endpoint exists, but no current public data was observed through it during testing.

---

## 6. Categories and Languages

### `GET mainCategories?lang={lang}`

Returns main categories.

Examples discovered:

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

### `GET subCategories?lang={lang}`

Returns additional/sub categories.

Examples discovered include:

```text
Biography
Family
History
Musical
War
Supernatural
Music
Talk-Show
Short
Reality-TV
Arabic dubbed
News
Ecchi
Film-Noir
Game
Psychological
Slice of Life
Game-Show
Magic
Super Power
Seinen
Shounen
School
Sports
Iraqi
Program
Religious
Biopic series
Dialogue
Entertaining
Bollywood
War & Politics
Reality
Soap
```

### `GET categoryLangFreqVideo`

Returns category/language frequency metadata.

Observed fields include:

```text
catNb
langNb
langFreq
langFreqSeries
lang_ar_title
lang_en_title
```

This endpoint provides the relationship between categories, languages, and movie/series frequencies.

### `GET videosByCategory?categoryID={categoryId}&orderby={orderBy}&videoKind={videoKind}&offset={offset}&level={level}`

Returns category-filtered videos.

Response shape:

```json
{
  "info": [ ... ]
}
```

Observed `videoKind` values:

```text
1 = Movies
2 = Series
```

The following ordering values were observed to work here:

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

### `GET videosByCategoryAndLanguage?...`

Full observed form:

```text
GET videosByCategoryAndLanguage?category_id={category_id}&language_id={language_id}&videoKind={videoKind}&orderby={orderBy}&level={level}&offset={offset}
```

Response shape:

```json
{
  "info": [ ... ]
}
```

This endpoint was confirmed to support category + language + movie/series filtering and the same family of ordering values listed above.

Example language IDs observed under an Action category included English, Arabic, Hindi, French, German, Italian, Spanish, Chinese, Japanese, Korean, Russian, Turkish, Norwegian, Persian, Swedish, and Hungarian.

---

## 7. Generic Video Listing and Pagination

### `GET video/V/2/itemsPerPage/{N}/level/{level}/videoKind/{videoKind}/sortParam/{sort}/pageNumber/{page}`

This is a general paginated video-listing endpoint.

Observed pagination behavior:

```text
itemsPerPage=12, page=0 -> 12 results
itemsPerPage=12, page=1 -> 12 results
itemsPerPage=30           -> 30 results
itemsPerPage=60           -> 60 results
```

Observed ordering values included:

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

### Important sorting caveat

Sorting behavior here is **not identical to `videosByCategory`**.

Observed during testing:

- `asc` / `desc` clearly changed the result set/order.
- `stars_asc` / `stars_desc` clearly changed the result set/order.
- `r_asc` / `r_desc` clearly changed the result set/order.
- `rating_asc` / `rating_desc` produced different result sets, but the exact backing field was not established.
- `views_asc` / `views_desc` produced different result sets, but the exact response field used for views was not exposed by the tested objects.
- `title_asc` and `title_desc` returned the same first-page result set/order in the test and therefore should **not** be treated as confirmed alphabetical sorting for this endpoint.

---

## 8. Groups and Collections

### `GET videoGroups/lang/{lang}/level/{level}`

Returns a wrapper object:

```json
{
  "groups": [ ... ]
}
```

Observed group fields include:

```text
groupsID
title
hasPagination
content
```

One tested `videoGroups` response contained 11 current groups.

### `GET videoListPagination/groupID/{groupId}/level/{level}/itemsPerPage/12/page/{page}`

Returns paginated content for a video group.

A tested group returned 12 items on page 0 and 2 items on page 1, confirming page-based pagination.

### Collections

The following collection-related endpoints were discovered:

```text
GET collectionsId/
GET collectionVideos/collectionID/{id}/level/{level}
```

The tested collection calls returned empty lists. No further probing was considered useful at this stage.

---

## 9. Staff

### `GET staff/actorID/{staffId}/lang/{lang}/level/{level}`

Returns staff information and associated video listings.

A tested staff ID for Minami Takayama returned a staff object plus 24 associated items.

Language selection affected localized titles/roles.

---

## 10. FAQ

### `GET faqQuestion`

Public endpoint returning FAQ data.

---

## 11. External Public Services

### Recommendations

```text
POST https://recommend.shabakaty.cc/api/recommendation/recommend
```

The service accepted a public recommendation request during testing and returned recommendation data.

Temporary recommendation/media URLs are intentionally omitted from this document.

### Ratings

```text
POST https://rating.shabakaty.cc/api/targets/rating-summary-by-targets-types
```

Observed request shape:

```json
{
  "targets": [
    {
      "targetId": "...",
      "targetType": "..."
    }
  ]
}
```

The tested service responded successfully, although one tested target returned no rating items:

```json
{
  "items": [],
  "totalCount": 0
}
```

A frontend `check-rating` endpoint was also observed in the JavaScript, but the live backend returned `405 Method Not Allowed` with an `Allow` set containing `DELETE,GET,OPTIONS` during testing. This indicates a frontend/backend mismatch or a method-specific implementation difference.

---

## 12. Useful Relationships

The public API can be viewed conceptually as:

```text
                         +----------------+
                         |     Banner     |
                         +-------+--------+
                                 |
              +------------------+------------------+
              |                  |                  |
              v                  v                  v
         Categories          Groups          Latest/Featured
              |                  |                  |
              v                  v                  |
       Category + Lang      Group Pagination        |
              |                  |                  |
              +------------------+------------------+
                                 |
                                 v
                         Video Listings
                                 |
                    +------------+------------+
                    |                         |
                    v                         v
                  Movie                    Series
                                               |
                                      +--------+--------+
                                      |                 |
                                      v                 v
                                    Seasons          Episodes
                                                         |
                                                         v
                                                  allVideoInfo
                                                         |
                             +---------------------------+-----------------------+
                             |             |             |           |         |
                             v             v             v           v         v
                          People       Categories      Ratings     Media    Playback
                                                                     |
                                                           +---------+---------+
                                                           |         |         |
                                                           v         v         v
                                                         Video   Subtitles  Thumbnails
```

---

## 13. Known Uncertainties

The following areas remain intentionally conservative in this reference:

1. Some numeric `status`/flag fields were recorded but not semantically decoded.
2. The exact meaning of every `kind` value in recommendation/related-video contexts was not exhaustively established.
3. `AdvancedSearch.sort` did not behave consistently enough to document as reliable.
4. `video/V/2` title sorting did not behave as expected in testing.
5. `popularFilms` exists, but the current tested public period/page combinations returned empty arrays.
6. Collection endpoints were reachable but returned empty arrays in testing.
7. Public comment testing returned an empty list, so the populated comment object schema remains unverified.

---

## 14. Notes on Stability and Reproduction

This is an **observed API map**, not an official API specification. Endpoint behavior, response fields, pagination rules, and backend implementations can change without notice.

For reproducible research:

- Prefer inspecting the frontend JavaScript alongside live requests.
- Treat response fields as implementation details unless confirmed by multiple endpoints.
- Do not hard-code temporary signed URLs.
- Do not publish authentication credentials discovered in client code.
- Re-test pagination and sorting if the frontend changes version.

---

## Endpoint Index

For quick reference:

```text
GET  allVideoInfo/id/{id}
GET  relatedVideos/id/{id}/kind/{kind}/level/{level}
GET  checkVideoParentalLevel/id/{id}
GET  transcoddedFiles/id/{id}
GET  translationFiles/id/{id}
GET  skippingDurations/id/{id}
GET  video_thumbnails/video_id/{id}
GET  videoComment/id/{id}/limit/{limit}/offset/{offset}
GET  videoSeasonNumber/id/{id}
GET  videoSeason/id/{id}

GET  banner/level/{level}
GET  videoGroups/lang/{lang}/level/{level}
GET  newlyVideosItems/level/{level}/offset/{offset}/
GET  latestVideosItems/level/{level}/offset/{offset}/
GET  latestMovies/level/{level}/itemsPerPage/24/page/{page}/
GET  latestSeries/level/{level}/itemsPerPage/24/page/{page}/
GET  featuredMovies/level/{level}/itemsPerPage/12/page/0
GET  popularFilms/kind/{period}/level/{level}/page/{page}
GET  videoListPagination/groupID/{groupId}/level/{level}/itemsPerPage/12/page/{page}
GET  collectionsId/
GET  collectionVideos/collectionID/{id}/level/{level}

GET  mainCategories?lang={lang}
GET  subCategories?lang={lang}
GET  categoryLangFreqVideo
GET  videosByCategory?categoryID={categoryId}&orderby={orderBy}&videoKind={videoKind}&offset={offset}&level={level}
GET  videosByCategoryAndLanguage?category_id={category_id}&language_id={language_id}&videoKind={videoKind}&orderby={orderBy}&level={level}&offset={offset}
GET  video/V/2/itemsPerPage/{N}/level/{level}/videoKind/{videoKind}/sortParam/{sort}/pageNumber/{page}
GET  video/V/2/itemsPerPage/{N}/video_title_search/{keyword}/itemsPerPage/{N2}/pageNumber/{page}/level/{level}
GET  AdvancedSearch?level={level}&{filters}
GET  staff/actorID/{staffId}/lang/{lang}/level/{level}
GET  faqQuestion

POST https://recommend.shabakaty.cc/api/recommendation/recommend
POST https://rating.shabakaty.cc/api/targets/rating-summary-by-targets-types
```

---

## Disclaimer

This document is intended as a technical research reference based on publicly observable behavior of the service. It is not an official Shabakaty/Cinemana API specification and should not be interpreted as a guarantee of future compatibility.