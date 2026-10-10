# 0008. Local AI order assistant that only suggests

- **Status:** Accepted
- **Date:** 2026-10-10

## Context

Admins take orders over the phone and type them into the "Nowe zamówienie" form: find the client, pick or add an
address, search every product. A note such as *"Nowe zamówienie dla Jana Kowalskiego, Grunwaldzka 15 mieszkania 4
w Gdańsku, telefon 600 100 200: sześć wód niegazowanych półtora litra i dwa chleby razowe"* already contains all of
it. A language model can read such notes (Polish and English), but:

- client data must not leave the machine and the project must stay free to run, so cloud AI APIs are out;
- small local models hallucinate – an invented client id, e-mail or price in an order is unacceptable;
- voice notes rarely contain everything the API requires (e-mail, postal code).

## Decision

- **Local only.** The model runs in Ollama (Docker, compose profile `ai`), called through
  `Microsoft.Extensions.AI` `IChatClient` (OllamaSharp). Default `gemma3:4b`; any model with structured output is a
  configuration change (`Assistant:Ollama:Model`, e.g. `gemma4:e4b`, `qwen3:4b`, Bielik 4.5B). The feature is off
  unless `Assistant:Enabled`; `/health` reports it as `Degraded`, never `Unhealthy`.
- **The model extracts, C# resolves.** The model gets only the note and returns *mentions* (names in the nominative,
  phone as digits, address parts, product phrases with quantities, courier notes) through a JSON schema at
  temperature 0. It never sees the database and returns no ids. A new stateless `Assistant` module matches the
  mentions deterministically (folded diacritics, Jaro-Winkler) through `ICustomerDirectory` and `ICatalogLookup`:
  phone/e-mail are certain, a single strong name match is selected, namesakes become a choice (the spoken address can
  break the tie), and products are selected only with a clear margin over the runner-up.
- **The admin always accepts.** `POST /api/assistant/order-drafts` returns a draft and writes nothing. The form shows
  what was filled in by AI, what is uncertain, and what will be **created** (new client, new shipping address).
- **Auto-create rules.** No match → a new client is proposed, still listing look-alikes ("Czy chodziło o…?").
  The username is generated (`jan.kowalski`, `jan.kowalski2`); an e-mail or phone is never invented – missing required
  fields stay empty and block the submit button. A spoken address that the client does not have becomes a new shipping
  address, completed by the geocoder (postal code, coordinates for a preview pin) when it is confident; a failed lookup
  only leaves the fields for the admin ([ADR 0006](0006-geocoding-never-blocks-saving.md)).
- **No cross-module transaction for accepting a draft.** The admin panel calls the existing endpoints in sequence:
  `POST /api/clients` (with the address) or `POST /api/clients/{id}/addresses`, then `POST /api/orders`. Created ids
  are kept in the page state, so retrying after a failed order (e.g. stock changed) does not create duplicates.

## Consequences

- Every write still goes through the validated, tested endpoints; the assistant adds no new way to corrupt data and
  can be switched off without touching the order flow.
- A client (or address) created for an order that then fails is a valid record, not garbage – acceptable, and simpler
  than a dedicated "create client + address + order" transaction across three modules.
- Matching is unit-testable without a model; model quality is measured separately (assistant evals).
- CPU-only inference takes seconds, so the endpoints are rate-limited per user and the panel shows progress.
- Fuzzy matching loads narrow projections of active clients/products into memory – fine for a small shop; a large one
  would pre-filter in SQL (e.g. `pg_trgm`) first.
