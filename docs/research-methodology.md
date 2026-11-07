# Xmana API Research Methodology

## Purpose

This document records how the Xmana public API reference was established so future contributors can distinguish observed facts from assumptions.

## 1. Static endpoint discovery

The Cinemana frontend bundle was inspected to discover:

- endpoint paths
- HTTP methods
- frontend parameter names
- fixed frontend values
- request construction
- search encoding
- sorting options
- relationships between series, seasons, episodes, staff, and ratings

The inspected frontend bundle was:

```text
https://cinemana.shabakaty.cc/main.d454b5f16dee02a7a9d8.js
```

Static discovery established a broader client API surface than Xmana actually needs. Account, telemetry, and personalized-state endpoints were intentionally separated from the public content API.

## 2. Live verification

Static findings were verified against the live service using read-only requests where possible.

Testing focused on:

- response type
- response envelope
- pagination
- sort behavior
- filters
- category/language relationships
- season/episode relationships
- empty-state behavior
- external public services

## 3. Representative fixtures

```text
Series:                    1119119
Episode:                   1119122
Detective Conan metadata: 192319
Minami Takayama staff:    2766
Animation category:        57
Crime category:            60
Action category:           84
Japanese language:         22
Emmy Winners group:        404
```

## 4. Search discovery

The frontend encodes the simple-search keyword with Base64URL without padding.

Example:

```text
Conan → Q29uYW4
```

The encoded value returned Conan/Detective Conan results. Raw unencoded search input did not provide the same reliable result set.

## 5. Pagination discovery

Different endpoints use `offset` differently.

Observed:

```text
newlyVideosItems              → count-like offset
latestVideosItems             → count-like offset
videosByCategory              → backend range/offset behavior
videosByCategoryAndLanguage   → backend range/offset behavior
video/V/2                     → pageNumber + itemsPerPage
latestMovies                  → page + fixed frontend page size
latestSeries                  → page + fixed frontend page size
videoListPagination           → page + fixed frontend page size
```

## 6. Sorting discovery

Sorting was compared between:

- `videosByCategory`
- `videosByCategoryAndLanguage`
- `video/V/2`
- `AdvancedSearch`

The same sort string cannot safely be assumed to mean the same thing on every endpoint.

In particular, `title_asc`/`title_desc` in `video/V/2` did not behave like reliable alphabetical ordering in the tested backend.

## 7. Ratings discovery

The frontend contains explicit target mapping logic for the public rating-summary service.

Observed:

```text
kind == "2" → Episode
otherwise   → Movie

rootSeries != "0" → targetId = rootSeries
rootSeries == "0" → targetId = nb
```

The request body was tested successfully.

## 8. Recommendation discovery

The recommendation body was extracted from frontend request construction and verified live:

```json
{
  "MovieId": "<video-id>",
  "MovieName": "<video-title>",
  "ReProcessIfExpired": true
}
```

## 9. Sensitive-output policy

The research intentionally did not publish:

- credentials
- client secrets
- passwords
- access tokens
- refresh tokens
- bearer tokens
- signed video URLs
- temporary signed image URLs
- personal account data

API shape was documented without reproducing sensitive values.

## 10. Network/research environment notes

During the original research environment, conventional UDP DNS resolution was unreliable. DoH was used to resolve the host, and the resolved Cinemana addresses were temporarily mapped through the local `/etc/hosts` environment for testing.

This was a **research-environment workaround**, not an Xmana application requirement.

A local ARM32 Katana executable also required a workaround during endpoint discovery. This is likewise a research-tooling note and is not part of the Xmana runtime architecture.

## 11. Maintenance procedure

When a new endpoint is discovered:

1. Add the exact path and method.
2. Record frontend parameter construction when known.
3. Verify a real response.
4. Record the response type/envelope.
5. Mark each semantic claim as verified, uncertain, unreliable, or empty.
6. Never invent undocumented fields or meanings.
7. Never publish credentials or temporary signed URLs.
8. Update `API.md` and `docs/api-verification-matrix.md` together.

The goal is to keep API research reproducible without requiring future contributors to repeat already-settled tests.
