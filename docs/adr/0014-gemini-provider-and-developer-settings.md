# 0014. Gemini as an optional assistant provider, chosen in developer settings

- **Status:** Accepted
- **Date:** 2026-11-15
- **Amends:** [0008](0008-local-ai-assistant-suggests-only.md) ("local only")

## Context

The local assistant ([ADR 0008](0008-local-ai-assistant-suggests-only.md)) works, but on a laptop CPU a draft takes
11–20 s and Whisper needs ~1 GB of RAM. English notes are its weak spot. A hosted model answers in about a second,
handles both languages better and needs no GPU. On the other hand it sends clients' names, phones, addresses and the
recordings to a third party and costs money per call. Which provider is right depends on the installation, and
switching it should not need a redeploy.

## Decision

- **Two providers behind the existing ports.** `IOrderExtractor` and `ISpeechToText` get a Gemini implementation
  next to Ollama and Whisper. Gemini receives the same prompt, examples and JSON schema (`responseJsonSchema`,
  temperature 0), so `OrderDraftResolver` and everything after extraction is unchanged: the model still only
  extracts, deterministic C# matches.
- **REST, not the Google SDK.** Two calls (`models.generateContent`, `models.get`) through a typed `HttpClient` with
  the standard resilience pipeline (attempt and total timeout, two retries on 429/5xx). Full control of the schema
  and audio parts, no extra dependency.
- **Developer settings in the database.** The Assistant module gets a schema `assistant` with a single-row
  `settings` table: switch, provider for notes (Ollama/Gemini), provider for dictation (off/Whisper/Gemini), Ollama
  and Gemini model. `IAssistantSettings` caches it for 30 s and invalidates it on save; per request, a selector
  delegates to the keyed implementation of the chosen provider. The configuration gives the starting values and
  stays the hard limit: `Assistant:Enabled`, `Assistant:Whisper:Enabled`, and Gemini only with an API key.
- **New role `Developer`, policy `DeveloperOnly`.** `GET/PUT /api/assistant/settings` and
  `POST /api/assistant/settings/test` (a fixed sample note, plus readiness of Whisper or one second of silence
  through Gemini). The panel page `/dev/assistant` is outside the menu: seven taps on the version line under it.
- **The API key never leaves the configuration.** Only user secrets or the environment
  (`Assistant__Gemini__ApiKey`); it is sent in the `x-goog-api-key` header, never in a URL, never stored, shown or
  logged. The settings only report whether a key exists.
- **Privacy is visible.** The developer page and the assistant panel say when notes or recordings go to Google.
  Only a key with billing enabled should be used (paid tier: Google does not train on the data). Prompts,
  answers and audio are not logged by either provider.
- **Whisper is loaded only when chosen.** `ISpeechToText.IsReadyAsync` replaces `PrepareAsync`: the first time a
  selected Whisper is asked (start-up, status poll or dictation), it downloads and loads its model in the background.

## Consequences

- An installation can run fully local (default), fully on Gemini, or mix them (e.g. Gemini for notes, Whisper
  for dictation), and switch without a restart. Several API instances agree within the cache time.
- Choosing Gemini sends personal data to Google: that is a decision for the data controller, documented in the
  security review; the default remains local.
- The evals run against either provider (`--provider gemini`); Gemini's accuracy has to be measured with a paid
  key before it is recommended.
- The settings are global state: integration tests that change them use their own database.
