# 0013. Security baseline: OWASP ASVS level 2

- **Status:** Accepted
- **Date:** 2026-11-14

## Context

KAMADA stores personal data of clients (names, addresses, phone numbers, e-mails) and lets workers change orders,
payments and delivery runs. Stage 2 added account features and hardening; it needs a yardstick to say what "secure
enough" means and to find what is missing, instead of a list of ad hoc fixes.

## Decision

- The target is **OWASP ASVS 4.0.3 level 2**, the level for applications with personal data and business transactions.
  Level 3 (high-value transactions, safety-critical systems) is not a goal.
- The review lives in [`docs/security.md`](../security.md): every applicable requirement with its status and where it is
  implemented, and a list of gaps (G1…G13) that become separate steps.
- Requirements that conflict with how the apps work are **accepted deviations**, written down with their mitigation:
  access tokens in `localStorage` (Blazor WebAssembly without a server; CSP without `unsafe-inline`, short-lived
  tokens), and access tokens valid up to 15 minutes after sign-out (stateless JWT, [ADR 0003](0003-jwt-with-rotating-refresh-tokens.md)).
- New features are checked against the same document; it is updated when a gap is closed or a new risk appears.

## Consequences

- Password and 2FA rules follow ASVS/NIST (length, blocklist, no composition rules, TOTP for administrators).
- Some gaps depend on the production setup (TLS, encrypted key ring, central logs) and are tracked for stage 5.
- The review is a self-assessment, not an audit or a penetration test.
