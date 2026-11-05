# Xmana Playback Notes

## Media source

Use GET /transcoddedFiles/id/{id} to discover available media qualities dynamically. Never assume every title exposes the same quality set.

The signed media URL property names were not included in the public API reference. The current implementation searches likely URL/file fields as a bootstrap fallback; this is not a verified stable response schema.

## Required player states

- loading and buffering
- playing and paused
- seeking and ended
- network failure and invalid media
- retry and quality fallback
- subtitle availability
- app suspend/resume

## Local progress

Persist playback position periodically, not for every media-position event. The target record should include video ID, series ID, season, episode, position, duration, last-played time and completion state.

## Resume

Offer Resume and Start Over only after a meaningful local position has been recorded.

## Subtitles and intro skip

Use only subtitle tracks and skip timings actually returned by the public API. Do not invent timings or assume a unit that has not been verified.

## Security

Never log, persist or expose temporary signed stream URLs.
