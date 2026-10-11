# 0003. JWT access tokens with rotating refresh tokens

- **Status:** Accepted
- **Date:** 2026-09-28

## Context

Two Blazor WebAssembly clients (admin panel, courier PWA) call the API from the browser. Couriers expect to
stay signed in on their phones for days; administrators and couriers must be revocable (deactivation,
password reset). There is no external identity provider in scope.

## Decision

- **Access token**: JWT (HMAC-SHA256), 15 minutes, claims `sub`, `name`, `given_name`, `family_name` and one
  `role` claim per role of the worker.
  Validated statelessly by the API; inbound claim mapping disabled so claim names stay short.
- **Refresh token**: 512-bit random opaque string, 14 days. Only its **SHA-256 hash** is stored
  (`workforce.refresh_tokens`) – a database leak does not expose usable tokens. A slow password hash is not
  needed because the token cannot be brute-forced.
- **Rotation**: every refresh revokes the presented token and issues a successor in the same *family*.
  Presenting an already rotated token is treated as theft: the entire family is revoked
  ([OAuth 2.0 Security BCP](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-security-topics) pattern).
- Deactivation and password reset revoke all refresh tokens of the worker. Already issued access tokens
  remain valid until they expire (≤ 15 min) – an accepted trade-off for stateless validation.
- Passwords are hashed with ASP.NET Core Identity's `PasswordHasher<T>` (PBKDF2, automatic rehash on login).
- Login and refresh are rate limited per client IP (fixed window, configurable).
- Login failures return the same error for unknown user, wrong password and deactivated account; a dummy
  hash is verified for unknown users so timing does not reveal valid usernames.
- Endpoints are **secure by default** (fallback policy requires an authenticated user); `AdminOnly` and
  `CourierOnly` policies restrict by role.

## Alternatives considered

- **Cookie authentication**: simpler XSS story, but cross-origin cookies between separately hosted SPAs and
  the API need `SameSite=None` + CSRF protection, and the PWA would still need long sessions.
- **ASP.NET Core Identity bearer tokens / external IdP (Keycloak, Entra ID)**: more features, but heavier to
  run in a one-command demo and hides the mechanics this project wants to show.

## Consequences

- Clients must refresh transparently (a delegating handler in both Blazor apps).
- Tokens are stored in browser storage by the SPAs; mitigated by short access token lifetime and rotation.

## Update 2026-10-11: workers with several roles

A worker may have several roles (e.g. an administrator who also delivers, see the
[phone app plan](../plans/mobile-app-roles.md)). The access token carries one `role` claim per role (a JSON array
in the payload); `RequireRole` policies match any of them, so `AdminOnly` / `CourierOnly` did not change. A role
change made by an administrator reaches the worker with the next refresh (≤ 15 min), like other changes to the
access token. Each frontend lists the roles it accepts and signs the user out when a refresh no longer carries any
of them.
