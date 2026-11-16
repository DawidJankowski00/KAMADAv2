# Security review – OWASP ASVS 4.0.3, level 2

- **Date:** 2026-11-14 (after the accounts and hardening work of stage 2)
- **Scope:** the API (`src/Kamada.Api` and the modules), the two Blazor WebAssembly apps behind nginx, the Docker
  Compose setup and CI. The production deployment (TLS proxy, servers, backups) is not built yet and is out of scope
  except where noted.
- **Target:** ASVS level 2 – the level for applications that handle personal data (client names, addresses and
  phone numbers) and business transactions. Decision recorded in [ADR 0013](adr/0013-security-baseline-asvs-level-2.md).

Status: **Met**, **Partial** (works, with a known limit), **Gap** (missing – listed at the end), **N/A** (does not apply).
Only the requirements that apply to this system are listed; the numbers are ASVS 4.0.3 requirement ids.

## V1 Architecture

| Req. | Status | How / decision |
|---|---|---|
| 1.1.2 Threat modelling | Gap | Trust boundaries are clear (browser apps → API → PostgreSQL, outbound to OSRM/Nominatim/Google/Ollama/Gemini/SMTP), but there is no written threat model. G8. |
| 1.2.2 Communication between components authenticated | Partial | API → PostgreSQL with a data-only role ([ADR 0012](adr/0012-least-privilege-database-role.md)). Inside one Docker network there is no TLS between containers; production TLS is stage 5. |
| 1.4.1 Access control enforced on a trusted service | Met | Every decision is made by the API; the apps only hide what the API would refuse. |
| 1.4.4 One access control mechanism | Met | ASP.NET Core authorization policies (`SignedIn`, `AdminOnly`, `CourierOnly`, `SalesAccess`) plus ownership checks in handlers. |
| 1.5.2 No unsafe deserialization | Met | System.Text.Json only, into fixed DTO types. |
| 1.14.1 Components separated by trust | Met | Modular monolith with enforced boundaries (architecture tests); frontends reference only contracts. |

## V2 Authentication

| Req. | Status | How / decision |
|---|---|---|
| 2.1.1 / 2.1.2 Length 12–128 | Met | `PasswordPolicy` (`MinLength` 12, `MaxLength` 128); checked whenever a password is set. |
| 2.1.3 / 2.1.4 No truncation, any Unicode | Met | Whole string hashed; no character restrictions. |
| 2.1.5 / 2.1.6 Users change their password, current one required | Met | `POST /api/auth/change-password`. |
| 2.1.7 Breached / common passwords refused | Partial | Built-in list of common passwords (≥ 12 characters) plus `Auth:PasswordPolicy:BlocklistFile` for a large breached-password list. No large list is bundled yet. G10. |
| 2.1.8 Strength meter | Gap | Only the length hint is shown. G9. |
| 2.1.9 No composition rules | Met | Removed in stage 2 (NIST SP 800-63B). |
| 2.1.10 No periodic rotation | Met | None. |
| 2.1.11 Paste and password managers | Met | Standard inputs with `autocomplete` (`current-password`, `new-password`, `one-time-code`). |
| 2.1.12 Show the typed password | Gap | No "show password" toggle. G9. |
| 2.2.1 Anti-automation | Met | Per-IP rate limit on every sign-in, reset and 2FA endpoint; account lockout after 5 wrong passwords or codes (15 min). Lockout answers like a wrong password. |
| 2.2.3 Notify after changes to credentials | Gap | No e-mail when the password, 2FA or the e-mail address changes. G1. |
| 2.3.1 Initial passwords | Gap | An admin types the first password of a worker and may reset it; the worker is not forced to change it. G3. |
| 2.4.1 Password storage | Met | ASP.NET Core Identity `PasswordHasher` (PBKDF2-HMAC-SHA512, salted, rehash on sign-in when the parameters change). |
| 2.5.1 / 2.5.6 Secure recovery | Met | Reset link by e-mail: random token, SHA-256 stored, 30 min, single use, newer request revokes older links; same answer whether or not the account exists, sent from a background queue (no timing difference). |
| 2.5.2 / 2.5.3 No hints, no revealing the password | Met | — |
| 2.5.4 No default accounts | Met | Demo accounts are seeded only with `Database:SeedDemoData` (Development). |
| 2.5.5 Notify when an authentication factor changes | Gap | See 2.2.3. G1. |
| 2.5.7 Lost OTP device | Met | Recovery codes; an admin can reset 2FA (signs the worker out everywhere). |
| 2.7 / 2.8 One-time passwords | Met | TOTP (RFC 6238), ±1 step, a code is accepted once (last step stored), secrets encrypted with Data Protection ([ADR 0011](adr/0011-two-factor-authentication.md)). Mandatory for administrators outside Development. |

## V3 Session management

| Req. | Status | How / decision |
|---|---|---|
| 3.1.1 No session tokens in URLs | Met | Bearer header only. The password reset link carries a one-time recovery token, not a session. |
| 3.2.1 New session on sign-in | Met | New refresh token family per sign-in (and per 2FA sign-in). |
| 3.2.2 ≥ 64 bits of entropy | Met | Refresh tokens: 512 random bits; stored as SHA-256. |
| 3.2.3 Tokens stored securely in the browser | Partial | Admin panel: `localStorage` (Blazor WebAssembly, no server to keep a cookie), exposed to XSS, which the CSP (`script-src` without `unsafe-inline`) and Blazor's output encoding mitigate. Phone app: Android Keystore-backed `SecureStorage` ([ADR 0015](adr/0015-native-phone-app-with-maui-blazor-hybrid.md)). G11. |
| 3.3.1 Sign-out invalidates the session | Partial | The refresh token is revoked at once; the access token stays valid until it expires (≤ 15 min), the accepted trade-off of stateless tokens ([ADR 0003](adr/0003-jwt-with-rotating-refresh-tokens.md)). |
| 3.3.2 Re-authentication (L2: 12 h or 30 min idle) | Gap | Refresh tokens are valid for 14 days and slide with use; there is no absolute session lifetime. G2. |
| 3.3.3 Sign out other sessions after a password change | Met | Automatic (change, reset and admin reset revoke every session). |
| 3.3.4 See and end own sessions | Met | "Moje sesje": device, IP, times; sign out one or all others. |
| 3.5.2 / 3.5.3 Signed, short-lived stateless tokens | Met | HS256 JWT, 15 min, issuer/audience/lifetime validated, unsigned tokens refused, only HS256 accepted (`ValidAlgorithms`). Closed G12. |
| 3.7.1 Re-authentication before sensitive operations | Partial | Own password change and turning 2FA off need the password (and a code). Admin actions on other accounts (reset password, reset 2FA) need only the admin's session. G13. |

## V4 Access control

| Req. | Status | How / decision |
|---|---|---|
| 4.1.1 / 4.1.3 Enforced on the server, least privilege | Met | Policies per endpoint; a test fails for any endpoint without a named policy or a deliberate `AllowAnonymous`, and for any unexpected anonymous one. |
| 4.1.4 Separation of duties for configuration | Met | Assistant provider settings need the separate `Developer` role (`DeveloperOnly`); an administrator without it gets 403. Secrets (the Gemini key) cannot be set through the API at all. |
| 4.1.5 Deny by default | Met | Fallback policy requires authentication; unknown routes 404. |
| 4.2.1 No direct object reference abuse | Met | Sellers see only their own orders (404 otherwise), couriers only their own runs, workers only their own sessions. |
| 4.2.2 CSRF | N/A | No cookies; bearer tokens are not sent automatically by the browser. |
| 4.3.1 MFA for administrative interfaces | Met | Administrators must enable 2FA (`Auth:TwoFactor:RequiredForAdmins`); enforced by the API for every endpoint outside `/api/auth`. |

## V5 Validation, sanitization and encoding

| Req. | Status | How / decision |
|---|---|---|
| 5.1.3 Input validated on the server | Met | FluentValidation on every request with a body or query; business rules in the domain. |
| 5.1.5 No open redirects | Met | The login page follows only local return URLs. |
| 5.2.x Sanitization of HTML | N/A | No user-supplied HTML is rendered (no `MarkupString`); Blazor encodes output. |
| 5.3.4 / 5.3.5 Parameterized queries | Met | EF Core LINQ only; `ILIKE` patterns escape `%`, `_`, `\` (`LikePattern`). |
| 5.2.6 SSRF | Met | Outbound requests go only to configured hosts (OSRM, Nominatim/Google, Ollama, Gemini, SMTP); no user-supplied URLs are fetched. Model names from the developer settings are validated and escaped into the Gemini path; the base URL is configuration only. |
| 5.5.x Deserialization | Met | See 1.5.2. |

## V6 Stored cryptography

| Req. | Status | How / decision |
|---|---|---|
| 6.2.1–6.2.5 Approved algorithms, no custom crypto | Met | PBKDF2, SHA-256, HMAC (JWT, TOTP), Data Protection (AES-256-CBC + HMAC-SHA256); libraries only. |
| 6.3.1 Secure random | Met | `RandomNumberGenerator` for tokens, recovery codes and TOTP secrets. |
| 6.4.1 / 6.4.2 Key management | Partial | JWT signing key from configuration: empty in `appsettings.json`, the API refuses to start without one of at least 32 characters, only Development ships a placeholder. Data Protection keys sit unencrypted in the database next to the data they protect. G7. |

## V7 Error handling and logging

| Req. | Status | How / decision |
|---|---|---|
| 7.1.1 / 7.1.2 No credentials or sensitive data in logs | Met | Ids only; e-mail addresses and recipients are not logged; e-mails log the subject only. |
| 7.1.3 Security events logged | Met | Failed sign-ins (with reason), lockouts, 2FA required/enabled/disabled, wrong codes, password changes and resets, token reuse (family revoked). |
| 7.1.4 Enough to investigate | Partial | Structured Serilog to the console; central collection and retention are stage 5. |
| 7.4.1 Generic errors | Met | ProblemDetails with codes; no stack traces outside Development. |

## V8 Data protection

| Req. | Status | How / decision |
|---|---|---|
| 8.1.1 Sensitive data not cached | Met | API responses default to `Cache-Control: no-store`; the phone's ETag lookups keep `private, no-cache` (revalidated every time). Closed G4. |
| 8.2.1 / 8.2.2 Browser storage | Partial | The phone keeps clients and products offline for sellers only, wiped on sign-out ([ADR 0009](adr/0009-offline-ordering-on-the-phone.md)), in a SQLite file in the app's private storage (not encrypted beyond Android's file-based encryption; backups of the app are disabled). |
| 8.3.1 Sensitive data in the body, not the URL | Met | Search terms in query strings are names, not credentials. |
| 8.3.x Personal data sent to third parties | Partial | Only when a developer selects Gemini for the assistant ([ADR 0014](adr/0014-gemini-provider-and-developer-settings.md)): notes (client names, phones, addresses) and recordings go to Google. Default is local (Ollama/Whisper); the developer page and the assistant panel say when data leaves; a paid key is required for no-training terms. Notes, answers and audio are not logged. |
| 8.3.4 Personal data identified | Partial | Clients (name, address, phone, e-mail) and workers; no retention or deletion policy yet (clients are deactivated, not deleted, because orders reference them). |

## V9 Communications

| Req. | Status | How / decision |
|---|---|---|
| 9.1.1 TLS for all traffic | Gap (stage 5) | Local development is plain HTTP. HSTS is already sent outside Development. TLS termination is stage 5 (Caddy). |
| 9.2.2 TLS to back-ends | Partial | SMTP supports STARTTLS/SslOnConnect; PostgreSQL inside the Docker network is not encrypted. |

## V10 Malicious code

| Req. | Status | How / decision |
|---|---|---|
| 10.3.2 Dependencies from trusted sources, integrity checked | Partial | NuGet with central versions and CI vulnerability checks; CLI scanner images pinned. Leaflet is loaded from jsDelivr without Subresource Integrity. G6. |

## V11 Business logic

| Req. | Status | How / decision |
|---|---|---|
| 11.1.1–11.1.4 Sequential steps, limits, anti-automation | Met | Order and run lifecycles are state machines (409 on an invalid transition); idempotent creates from the phone; rate limits on authentication and the AI assistant. |

## V12 Files

| Req. | Status | How / decision |
|---|---|---|
| 12.1.1 / 12.2.1 Size and type of uploads | Met | Dictation: WAV PCM 16 kHz mono, ≤ 60 s, ≤ 2 MB, validated before Whisper; nothing is stored. |

## V13 API

| Req. | Status | How / decision |
|---|---|---|
| 13.1.3 No sensitive data in URLs | Met | See 3.1.1. |
| 13.2.1 Only the methods the apps use | Met | CORS allows GET/POST/PUT/DELETE; each route is mapped to one method. |
| 13.2.5 Content type checked | Met | Minimal APIs bind JSON bodies only from `application/json`. |

## V14 Configuration

| Req. | Status | How / decision |
|---|---|---|
| 14.1.x Build pipeline | Met | Warnings as errors in production code, tests, CI on every push and weekly. |
| 14.2.1 / 14.2.3 Up-to-date components, CDN integrity | Partial | CI fails on vulnerable NuGet packages and on fixable high/critical image vulnerabilities (Grype); images build with `--pull`. Leaflet without SRI (G6). |
| 14.3.2 No debug features in production | Met | Scalar/OpenAPI not mapped in Production; exception details only in Development (`GlobalExceptionHandler`). |
| 14.3.3 No version information in headers | Met | nginx `server_tokens off`; Kestrel with `AddServerHeader = false`. Closed G5. |
| 14.4.1–14.4.7 Security headers | Met | API: `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, CSP `default-src 'none'`, HSTS. Apps: CSP with hashes and the API origin, `nosniff`, framing denied, referrer and permissions policies. |
| 14.5.3 CORS allowlist | Met | Exact origins from configuration, validated at start-up; no credentials. |
| Secrets in the repository | Met | gitleaks over the whole history in CI; only test/dev placeholders allowed. |

## Gaps (proposed follow-up steps)

| Id | ASVS | What to do | Effort |
|---|---|---|---|
| G1 | 2.2.3, 2.5.5 | E-mail the worker when the password, the e-mail address or 2FA changes (through the existing queue). | small |
| G2 | 3.3.2 | Absolute session lifetime: a refresh token family ends after N hours of use (12 h at L2) or 30 min idle, configurable per role (couriers work full shifts). | medium |
| G3 | 2.3.1 | Force a password change at the first sign-in after an admin set or reset the password. | medium |
| ~~G4~~ | 8.1.1 | Done 2026-11-14: `no-store` by default on `/api`. | – |
| ~~G5~~ | 14.3.3 | Done 2026-11-14: no `Server` header from Kestrel. | – |
| G6 | 10.3.2, 14.2.3 | Leaflet: Subresource Integrity on the jsDelivr files, or serve them from the app. | small |
| G7 | 6.4.1 | Encrypt the Data Protection key ring at rest (certificate or key vault) in production. | stage 5 |
| G8 | 1.1.2 | A short threat model (data flows, trust boundaries, abuse cases) in `docs/`. | small |
| G9 | 2.1.8, 2.1.12 | "Show password" toggle and a strength hint on the password forms. | small |
| G10 | 2.1.7 | Ship or configure a large breached-password list (licence to check, e.g. SecLists – MIT). | small |
| ~~G11~~ | 3.2.3 | Phone: done 2026-11-16 (MAUI secure storage). Accepted for the web admin panel (CSP mitigates). | done |
| ~~G12~~ | 3.5.3 | Done 2026-11-14: only HS256 accepted. | – |
| G13 | 3.7.1 | Ask for the admin's password or code before resetting another worker's password or 2FA. | medium |
