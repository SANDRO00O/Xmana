# Security and Responsible Disclosure

This repository documents a public/read-oriented API surface. It intentionally excludes credentials, access tokens, refresh tokens, client secrets, user-specific data, and temporary signed media URLs.

## Do not commit

- API credentials or client secrets
- Bearer/access/refresh tokens
- Session cookies
- User account data
- Private URLs or temporary signed media URLs
- Local environment files containing secrets

## If you discover a credential or private data

Do not open a public issue containing the secret. Remove it from repository history where appropriate and report the exposure privately through the repository owner's preferred private contact channel.

## Scope

This project is not affiliated with Shabakaty or Cinemana. It implements only the public/read-oriented catalog surface and does not provide access to private user data or private account functionality.
