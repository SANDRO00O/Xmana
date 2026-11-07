# Xmana Playback Notes

The player is a first-class Xmana subsystem, not a thin wrapper around default controls.

## Media source

Use `GET /transcoddedFiles/id/{id}` to discover available media qualities dynamically.

Never assume that every video exposes the same qualities.

## Required states

The player must handle:

- loading
- buffering
- playing
- paused
- seeking
- ended
- network failure
- invalid media
- retry
- quality fallback
- subtitle availability
- app suspend/resume

## Local progress

Persist playback progress periodically, not on every media-position event.

Store at least:

```text
videoId
seriesId
season
episode
position
duration
lastPlayed
completed
```

## Resume

Offer `Resume` and `Start Over` when a meaningful local position exists.

## Subtitles

Use only subtitle tracks actually returned by the public API.

## Intro skip

Use skip timing data only when the backend provides it. Do not invent timings or assume a time unit that has not been verified.

## Security

Never log, persist, or expose temporary signed stream URLs.
