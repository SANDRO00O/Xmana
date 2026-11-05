# Xmana Troubleshooting

## Catalog returns no items

An empty list is not automatically an application error. Check API.md and the verification notes. Some endpoints returned empty responses during the observed test period.

## Sorting behaves unexpectedly

Do not assume sort semantics are universal. Generic listing, category listing and AdvancedSearch did not behave identically in the verification pass.

## Playback URL fails

Refresh transcoddedFiles and rediscover currently available qualities. Never hard-code a signed media URL. The URL parser is currently best-effort and needs validation against a fresh response.

## No subtitles or intro skip

The translation endpoint may legitimately return empty fields. skippingDurations may return empty arrays. Hide unavailable controls instead of fabricating data.

## Season posters unavailable

The separate season-poster endpoint returned HTTP 503 during the original research period. Core series browsing must not depend on it.

## Focus/navigation issue

Check the shared focus behavior before adding control-specific workarounds. Test top tabs, horizontally scrolling rails, dialogs and return-focus behavior with the D-pad.

## Arabic layout issue

Verify that the whole hierarchy switches to RTL, not only the text. Search, details, player controls, lists, dialogs, focus flow and spacing all require Arabic review.
