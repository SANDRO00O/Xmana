# API implementation notes

`API.md` and `docs/api-verification-matrix.md` are the source of truth. This file records how each behavior is
implemented and which parts are **unknown** and therefore handled defensively. Nothing here is a question for the
project owner: unknown means "tolerant model + raw value preserved", per API.md section 17.

## Confirmed behavior (implemented exactly)
| Area | Implementation |
|---|---|
| Recommendations | `POST` body `{ MovieId, MovieName, ReProcessIfExpired: true }` built by `GetRecommendationsAsync(videoId, title)`. Never cached, never logged. Optional: failure falls back to `relatedVideos` (Milestone 5). |
| Ratings | `RatingTarget.For(kind, rootSeries, nb)`: `kind == "2"` -> `Episode`, otherwise `Movie`; id = `rootSeries` unless it is empty or `"0"`, then `nb`. Empty `items` means "no rating", never a fake value. |
| Simple search | Route `video/V/2/itemsPerPage/20/video_title_search/{base64url}/itemsPerPage/{pageSize}/pageNumber/{n}/level/{l}` (first `itemsPerPage` is the frontend constant 20). |
| Advanced search | Filters sent only when set; `year=from,to`; `category_id=a,b` (OR); `type=movies|series`; `sort` is passed through but is **unreliable** and never promised in the UI; `staffTitle` is **not reliable** as an exact lookup. |
| Categories | `mainCategories`/`subCategories` entries use `nb` as id. |
| Category languages | `categoryLangFreqVideo` returns category objects with `langArray` (`langNb`, `langFreq`, `langFreqSeries`, titles). `langNb` is the `language_id` for `videosByCategoryAndLanguage`. |
| `offset` semantics | Per endpoint, never unified: count for `newlyVideosItems`/`latestVideosItems` (`offsetAsCount`), range/offset for category endpoints, page for `latestMovies`, `latestSeries`, `videoListPagination`, `video/V/2`, `AdvancedSearch`. |
| Episodes | `videoSeason` order is not chronological: group by `season`, order by `episodeNummer` (`CatalogMapper.GroupBySeason`). Seasons come from `videoSeasonNumber` (`season`). |
| Translations | Only non-empty `*TranslationFile` values are tracks; empty is a normal result (subtitle controls hidden). The `*Path` placeholder (`defaultImages/loading.gif`) is not a track. |
| Thumbnails | `{ videoThumbnails: [], status: 204 }` is a valid empty result. |
| Featured Series | **Observed live behavior**: the `videoGroups` group with `groupsID` `2` (observed title `Featured Series`, `hasPagination` 1, 12 items). Rendered as its own rail right after Featured Movies, paginated through `videoListPagination/groupID/2/...`. No `/featuredSeries` endpoint exists and `collectionVideos/collectionID/2` is empty, so neither is used. The rail is optional: absent when the group is absent or empty. |
| Popular / collections | Valid endpoints that are currently empty: empty is a normal result and the UI hides the section. |

## Browse, search and Anime behavior implemented
| Item | Implementation |
|---|---|
| Sort picker | Offers only `desc`/`asc` (labelled "Default order"/"Reverse order") and `stars_desc`/`stars_asc` ("Highest/Lowest rated"). Title, rating, views and `r_*` values are accepted by the API layer but never offered because their meaning was not established. Advanced search defaults to no sort. |
| Anime | Category `57` (Animation); Japanese language `22` is applied by default only if `categoryLangFreqVideo` lists it for the chosen kind. Movies and Series both populated in testing, so both are offered. Observed live ids, not constants. |
| Language filter | Built from `langArray` per category; languages with a zero count for the selected kind are hidden. Without language metadata no filter is shown. |
| Search | 450 ms debounce, pages of 12 (`itemsPerPage` second segment), first `itemsPerPage` is the constant 20. |

## Unknown or unreliable (handled defensively)
| Item | Handling |
|---|---|
| `transcoddedFiles` signed-URL field names | Qualities are detected by value (`<height>p` label + HTTPS URL in the same object, or an object keyed by such a label). Never assume all five qualities. Never cached, logged or persisted. |
| Skip `start[]`/`end[]` unit | Index-matched pairs with `start < end`; **no unit conversion is applied**. A player may only use skip data after the unit is established; until then the Skip Intro control stays hidden. |
| Parental `status` meaning | Raw value preserved; nothing is gated on it. |
| `relatedVideos` `kind` meaning | The video's own `kind` string is passed. |
| Comment item schema | Raw objects exposed; comments are not displayed. |
| FAQ item schema | Read defensively by property names containing question/answer (interface-language variant preferred); nothing readable means the FAQ screen reports it as unavailable. Replace with a typed mapping once a response is captured. |
| Recommendation item schema | Id read from `nb` or `MovieId`, title from `en_title` or `MovieName`; raw kept in memory only; items without an id are dropped. |
| `staff` payload layout | Staff object located structurally; associated titles from the item list; raw kept. |
| `r_asc`/`r_desc`, generic `title_*` sorting, `AdvancedSearch.sort` | Values are sent only from the documented family; labels never claim a specific field. |

## Playback behavior implemented (milestone 6)
| Item | Implementation |
|---|---|
| `transcoddedFiles` | Qualities detected by value; the quality list shown is exactly what was returned; the signed address lives only inside the engine hand-off. Re-requested after every failure. |
| Subtitles | `translationFiles` is requested for every playback; a track needs a non-empty file that is an absolute HTTPS URL; the `*Path` placeholder is ignored. |
| Skip intro | Requested and parsed, but **not shown** until `SkipController.VerifiedUnit` is set after a populated response confirms the unit. |
| Thumbnails | Not used by the player (always empty in testing); the scrubber shows position only. |
| Parental status | Not requested by the player; no content is blocked on an unestablished meaning. |

## Deliberately not implemented
- `POST /getVideosByIds`: discovered but its response schema was not live-verified.
- `thumbnail.shabakaty.cc` season posters: returned 503; optional, never needed for navigation.
- `POST /api/ratings/check-rating`: returned 405.
- All authenticated, account, history, subscription, notification and telemetry endpoints (API.md section 19).

## Safety rules enforced by tests
- Signed `*ObjUrl` fields are stripped before anything is written to disk; media, subtitles, skip data, thumbnails, parental status, comments and recommendations are never cached.
- Logs and exception messages never contain URLs.
- POST requests are never retried; GET retries are bounded with backoff.
- `ICinemanaApi` has no authentication, account or write methods.
