# Xmana API Verification Matrix

**Verification date recorded by the source research:** 2026-09-30  
**Scope:** public/read-oriented API behavior only.

| Area | Endpoint | Observed behavior | Caveat |
|---|---|---|---|
| Video metadata | GET allVideoInfo/id/{id} | Rich video metadata object | Fields can vary |
| Related videos | GET relatedVideos/id/{id}/kind/{kind}/level/{level} | List of related video objects | Not every kind value was exhaustively defined |
| Parental status | GET checkVideoParentalLevel/id/{id} | Status object | Numeric meaning not independently decoded |
| Video files | GET transcoddedFiles/id/{id} | Quality entries including 240p through 1080p | Signed media URLs are intentionally omitted |
| Translations | GET translationFiles/id/{id} | Arabic/English/Spanish fields and paths | Actual subtitle download paths may vary |
| Skip durations | GET skippingDurations/id/{id} | start/end arrays | Tested response was empty; units not established |
| Thumbnails | GET video_thumbnails/video_id/{id} | Thumbnail metadata wrapper | Tested response was empty with status 204 |
| Comments | GET videoComment/id/{id}/limit/{limit}/offset/{offset} | Public comments list | Tested response was empty; populated schema unknown |
| Seasons | GET videoSeasonNumber/id/{id} | Season entries | Four entries observed in one series test |
| Episodes | GET videoSeason/id/{id} | Episode list | 86 episodes observed in one series test |
| Simple search | GET video/V/2/.../video_title_search/... | Base64URL-without-padding query | Search route has two itemsPerPage segments |
| Advanced search | GET AdvancedSearch?level={level}&{filters} | Title/type/year/category filters | staffTitle and sort are unreliable |
| Banner | GET banner/level/{level} | Direct list | 44 items in tested level=0 response |
| Groups | GET videoGroups/lang/{lang}/level/{level} | groups wrapper | 11 groups observed in test |
| Newest videos | GET newlyVideosItems/.../offset/{offset}/ | Count-like offset behavior | Not a conventional skip offset |
| Latest videos | GET latestVideosItems/.../offset/{offset}/ | Count-like offset behavior | Not a conventional skip offset |
| Latest movies/series | GET latestMovies and latestSeries | Paginated 24-item pages | Use the exact path shapes in API.md |
| Featured movies | GET featuredMovies/... | 12 items in test | Frontend page fixed at 0 |
| Popular films | GET popularFilms/kind/{period}/... | Endpoint exists | M/W/D combinations tested returned empty lists |
| Categories | GET mainCategories and subCategories | Category lists | Language parameter affects titles |
| Category/language | GET videosByCategoryAndLanguage | Combined category/language/movie-series filtering | videoKind 1=movies, 2=series on tested listing endpoints |
| Generic listing | GET video/V/2/... | Page-size/pagination and sort | title_asc/title_desc were not reliable |
| Group paging | GET videoListPagination/groupID/... | Page-based list | 12 items then 2 items in tested group |
| Collections | GET collectionsId and collectionVideos | Endpoints reachable | Tested calls returned empty lists |
| Staff | GET staff/actorID/{staffId}/lang/{lang}/level/{level} | Staff profile and associated works | Language affects localized values |
| FAQ | GET faqQuestion | Reachable FAQ endpoint | Full schema not exhaustively documented |
| Recommendations | POST recommendation service | Exact body uses MovieId, MovieName, ReProcessIfExpired | Optional service |
| Rating summary | POST rating-summary-by-targets-types | targets array with targetId/targetType | Empty items are valid |
| Bulk lookup | POST /getVideosByIds | Form-urlencoded ids field discovered in frontend | Response schema not live-verified |
| Season posters | POST thumbnail seasonposters | RootSeries request observed | Returned HTTP 503 during research |

## Implementation rules

- Do not infer semantics from undocumented numeric flags.
- Treat empty lists and 204-style results as legitimate API outcomes.
- Do not assume sorting or offset semantics are shared between endpoints.
- Do not depend on bulk lookup or season-poster services until their response contracts are verified.
- Do not hard-code or persist signed media URLs.
- Keep login, account settings, subscriptions, notifications, history and account-changing requests outside the public-only runtime.
