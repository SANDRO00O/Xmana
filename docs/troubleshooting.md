# Xmana Troubleshooting

## API returns empty data

An empty list is not automatically an application error.

Check `API.md` first. Some endpoints were observed returning empty responses during the live verification period, including `popularFilms` and collection endpoints.

## Sorting behaves unexpectedly

Do not assume sorting semantics are universal. `video/V/2`, `videosByCategory`, `videosByCategoryAndLanguage`, and `AdvancedSearch` do not all behave identically.

## Playback URL fails

Refresh `transcoddedFiles` and rediscover currently available qualities. Never hard-code a signed media URL.

## No subtitles

The API may legitimately return empty translation fields. Hide subtitle controls instead of treating the response as malformed.

## No intro skip

`skippingDurations` may legitimately contain empty arrays. Do not invent timings.

## Recommendation service unavailable

Recommendations are optional. Fall back to `relatedVideos` or hide the section.

## Season posters unavailable

The separate season-poster service returned HTTP 503 during the research period. Xmana must not depend on it for core series navigation.

## Focus/navigation issue

Check the central focus system before patching individual controls. Ensure modal focus trapping and focus restoration are still deterministic.

## Arabic layout issue

Verify that the entire visual hierarchy switches to RTL rather than only translating text. Search, player controls, lists, dialogs, focus order, and spacing must all be tested in Arabic.
