# Xmana API Verification Matrix

This matrix is a compact implementation-oriented companion to `API.md`.

**Live verification date:** 2026-09-30  
**Scope:** public/read-oriented API behavior only.

| Area | Endpoint | Verification | Key observed result | Implementation status |
|---|---|---|---|---|
| Video metadata | `GET /allVideoInfo/id/{id}` | Live | Rich video object; known fields documented in `API.md` | Verified |
| Related videos | `GET /relatedVideos/id/{id}/kind/{kind}/level/{level}` | Live | Direct list; 11 items for tested `1119122/kind=1/level=0` | Verified |
| Parental status | `GET /checkVideoParentalLevel/id/{id}` | Live | `{status:"1"}` for `1119122`; semantic meaning unknown | Verified response / uncertain meaning |
| Video files | `GET /transcoddedFiles/id/{id}` | Live | 240p/360p/480p/720p/1080p observed; signed URLs omitted | Verified |
| Translation files | `GET /translationFiles/id/{id}` | Live | Arabic/English/Spanish fields plus paths | Verified |
| Skip durations | `GET /skippingDurations/id/{id}` | Live | `{start:[],end:[]}` | Verified response / unit unknown |
| Thumbnails | `GET /video_thumbnails/video_id/{id}` | Live | `{videoThumbnails:[],status:204}` | Verified |
| Comments | `GET /videoComment/id/{id}/limit/{limit}/offset/{offset}` | Live | Empty list for tested `1119122` | Verified empty result / populated schema unknown |
| Seasons | `GET /videoSeasonNumber/id/{id}` | Live | Four seasons for `1119119` | Verified |
| Episodes | `GET /videoSeason/id/{id}` | Live | 86 episodes for `1119119` | Verified |
| Simple search | `GET /video/V/2/.../video_title_search/...` | Live + JS | Base64URL-no-padding keyword; `Conan` → `Q29uYW4` | Verified |
| Advanced title search | `GET /AdvancedSearch?...videoTitle=...` | Live | `Conan` returned Conan/Detective Conan results | Verified |
| Advanced type | `AdvancedSearch type=` | Live | `movies` and `series` work | Verified |
| Advanced year | `AdvancedSearch year=` | Live | Inclusive ranges observed | Verified |
| Advanced category | `AdvancedSearch category_id=` | Live | Comma-separated IDs behave OR-like | Verified |
| Advanced staff | `AdvancedSearch staffTitle=` | Live | Filter exists but exact-name behavior unreliable | Uncertain |
| Advanced sort | `AdvancedSearch sort=` | Live | Not reliably distinct | Unreliable |
| Banner | `GET /banner/level/{level}` | Live | 44 items at level 0 | Verified |
| Video groups | `GET /videoGroups/lang/{lang}/level/{level}` | Live + JS | `{groups:[...]}`, 11 groups in test | Verified |
| Newly videos | `GET /newlyVideosItems/.../offset/{offset}/` | Live | Offset behaves as count/limit with cap | Verified unusual semantics |
| Latest videos | `GET /latestVideosItems/.../offset/{offset}/` | Live | Offset behaves as count | Verified unusual semantics |
| Latest movies | `GET /latestMovies/.../page/{page}/` | Live | 24 items per frontend request | Verified |
| Latest series | `GET /latestSeries/.../page/{page}/` | Live | 24 items per frontend request | Verified |
| Featured movies | `GET /featuredMovies/.../page/0` | Live | 12 items | Verified |
| Popular films | `GET /popularFilms/...` | Live | M/W/D page tests returned empty lists | Verified endpoint / currently empty |
| Main categories | `GET /mainCategories?lang=` | Live | Category list including IDs documented in `API.md` | Verified |
| Sub categories | `GET /subCategories?lang=` | Live | Subcategory list including IDs documented in `API.md` | Verified |
| Category-language metadata | `GET /categoryLangFreqVideo` | Live | `langArray` with frequencies and language IDs | Verified |
| Category videos | `GET /videosByCategory` | Live | `info` envelope; sort family works | Verified |
| Category + language | `GET /videosByCategoryAndLanguage` | Live | `info` envelope; Japanese animation test worked | Verified |
| Generic listings | `GET /video/V/2/...` | Live | ItemsPerPage 12/30/60 honored | Verified |
| Generic sorting | `sortParam=` | Live | stars/r/asc/desc work; title sorting unreliable | Partially verified |
| Group pagination | `GET /videoListPagination/...` | Live | Group 404 page0=12, page1=2 | Verified |
| Collections list | `GET /collectionsId/` | Live | `[]` | Verified empty |
| Collection videos | `GET /collectionVideos/...` | Live | Tested IDs returned empty | Verified empty in tests |
| Staff | `GET /staff/actorID/...` | Live | Staff + associated works; Minami Takayama example | Verified |
| FAQ | `GET /faqQuestion` | Live | Reachable and returned FAQ data | Verified reachability / schema incomplete |
| Recommendations | `POST /api/recommendation/recommend` | Live | Body `{MovieId,MovieName,ReProcessIfExpired:true}` → HTTP 200 | Verified |
| Rating summary | `POST /api/targets/rating-summary-by-targets-types` | Live | Target body confirmed; HTTP 200 | Verified |
| Rating check | `POST /api/ratings/check-rating` | Live | HTTP 405; backend allowed DELETE/GET/OPTIONS | Do not depend on |
| Bulk video lookup | `POST /getVideosByIds` | JS extraction | Form-urlencoded body with `ids`; response schema not captured | Discovered / not live-schema-verified |
| Season posters | `POST /api/series/seasonposters` | Live | `{RootSeries:<id>}` pattern; HTTP 503 | Optional/unavailable |

## Core test fixtures

```text
Series:                   1119119
Episode:                  1119122
Detective Conan metadata: 192319
Staff (Minami Takayama):  2766
Animation category:       57
Crime category:           60
Action category:          84
Japanese language:        22
Group:                    404
```

## Confidence rules

- **Verified** means the exact endpoint behavior was observed live.
- **Verified response / uncertain meaning** means the response is known but a semantic interpretation is not.
- **Unreliable** means the route exists but tests did not establish stable semantics.
- **Currently empty** means the endpoint worked but returned no records at the time of testing.

Do not turn an uncertain item into an assumed specification.
