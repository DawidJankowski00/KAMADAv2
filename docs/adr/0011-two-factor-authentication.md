# 0011. Two-factor authentication with TOTP

- **Status:** Accepted
- **Date:** 2026-11-14

## Context

Accounts are protected only by a password ([ADR 0003](0003-jwt-with-rotating-refresh-tokens.md)). Administrators can
see every client's address and phone number and change orders and payments, so a guessed or reused password is the
main risk once the system runs publicly. Workers already carry a phone, and the system must not depend on SMS or a paid
service.

## Decision

- **TOTP** (RFC 6238: HMAC-SHA1, 6 digits, 30-second steps) with any authenticator app; library Otp.NET (MIT). A code
  is accepted one step early or late for clock drift, and only once: the step of the last accepted code is stored, so a
  code seen over the worker's shoulder cannot be replayed.
- **Secrets are encrypted** with ASP.NET Core Data Protection (keys in `workforce.data_protection_keys`, shared by every
  API instance). Setup is two steps: `setup` creates a pending secret, `enable` turns 2FA on only after a code from it
  proves the app was set up.
- **Ten recovery codes** (`xxxxx-xxxxx`, no look-alike characters) are shown once when 2FA is enabled; only SHA-256
  hashes are stored, each works once, a new set replaces the old one. An administrator can turn 2FA off for a worker who
  lost both (`POST /api/workers/{id}/reset-two-factor`, signs them out everywhere).
- **Two-step sign-in.** `POST /api/auth/login` answers `LoginResponse`: tokens, or for an account with 2FA a challenge
  (Data Protection, time-limited to 5 minutes, carries only the worker id). `POST /api/auth/login/two-factor` takes the
  challenge and a code. The challenge is stateless, so the number of attempts is limited by the account lockout: wrong
  codes count like wrong passwords, and the correct password does **not** reset the count while a code is pending
  (otherwise the right password between guesses would allow unlimited guessing).
- Turning 2FA off needs the password **and** a code, so neither a stolen session nor a stolen password alone does it.

## Alternatives considered

- **SMS or e-mail codes:** cost money (SMS) or depend on the same mailbox as the password reset, and SMS can be
  intercepted (SIM swap).
- **WebAuthn / passkeys:** phishing-resistant and the better long-term option, but the phone app is moving to .NET MAUI
  (stage 4), where platform passkey support needs native work in each platform; TOTP works the same everywhere today.
- **ASP.NET Core Identity's 2FA:** the project does not use Identity's user store (only its `PasswordHasher`), and
  bringing it in for 2FA would change the whole account model.

## Consequences

- Clients must handle a login that returns a challenge instead of tokens (`LoginResponse`).
- Data Protection keys sit in the same database as the secrets they protect; a full database dump exposes both.
  Encrypting the key ring (certificate or key vault) is left to the production setup.
- A worker who loses the phone and the recovery codes needs an administrator.
