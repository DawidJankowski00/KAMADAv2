# 0010. On-device assistant as a fallback

- **Status:** Accepted (default models provisional until the phone measurements in [on-device-assistant.md](../on-device-assistant.md))
- **Date:** 2026-10-11

## Context

The order assistant ([ADR 0008](0008-local-ai-assistant-suggests-only.md)) runs on the server: Whisper.net for dictation,
Ollama for the language model. A seller on a poor connection (the case [ADR 0009](0009-offline-ordering-on-the-phone.md)
solved for the order form itself) cannot reach it, although dictating an order is exactly what is convenient on a phone.
Small models now run in a mobile browser (WebGPU), and the part that decides what ends up in an order is deterministic C#
that can run in WebAssembly too.

## Decision

- **The server stays the default.** `RoutingOrderAssistant` (the phone app's `IOrderAssistant`) uses it while the
  connection is `Good` and the server reports the assistant enabled. It switches to the model on the phone when the
  connection is `Poor` or `Offline`, when the server answers 502/503/504 or does not answer (transport error, timeout), or
  when the worker chose "Zawsze telefon". Validation and permission errors do not trigger the fallback: they would be the
  same on the phone. The rule is `AssistantPolicy`, a pure function with unit tests. A chip over the panel says which
  assistant works and lets the worker choose (automatic / server / phone).
- **Same pipeline, same answers.** The extraction prompt, JSON schema and answer cleaning, the text matching, the client /
  address / product matchers and the draft resolver moved to `Kamada.Shared.Matching` (no EF, no ASP.NET; referenced by the
  server modules and by `Kamada.Web.Shared`). The resolver reads its data through `IOrderDraftSource`: the server
  implements it over the Customers and Catalog contracts and the geocoder, the phone over its local copy of clients and
  products ([ADR 0009](0009-offline-ordering-on-the-phone.md)). The result is the same `OrderDraftDto`, so the form and the
  outbox behave exactly as with the server. Geocoding is not available without a connection: a new address keeps what was
  said and the worker is warned.
- **Models in the browser.** A language model through WebLLM (WebGPU, MLC format, JSON-schema grammar, temperature 0) and
  Whisper through transformers.js / ONNX Runtime Web (WebGPU, WebAssembly otherwise). Without WebGPU and 16-bit shaders the
  language models cannot run: the option is hidden and the phone keeps using the server.
- **Opt-in download, own origin.** Models are large (hundreds of MB to ~1.7 GB), so the worker downloads them on a settings
  page (progress, storage used, delete, "keep my data" via `navigator.storage.persist()`). Files live in Cache Storage;
  nothing is downloaded in the background. They are served by the phone app's nginx under `/models`
  (`deploy/assistant/fetch-models.sh`, compose profile `assistant`), and the pinned libraries come from
  `deploy/assistant/fetch-runtime.sh` (npm tarballs verified against their sha512) and are cached by the service worker on
  first use. At runtime the phone talks only to our own server, never to a CDN.
- **Privacy.** On the phone nothing leaves the device: not the recording (it is only decoded in memory), not the text, not
  the client data used for matching. The assistant works for the Seller role only, as that role holds the local copy; an
  admin using the phone stays online-only, as before.
- **Memory.** One language model and one speech model are loaded at a time unless the device reports at least 8 GB
  (`navigator.deviceMemory`); loading the other unloads the first, and the files stay cached.

## Consequences

- A seller can dictate or type an order with no connection at all and the draft is checked by the same rules as on the
  server; the quality of the extraction depends on a much smaller model, so the draft is still only a suggestion.
- Models must be put on the server once (`docker compose --profile assistant up assistant-models`); without them the
  settings page offers nothing to download and the server assistant is used as before.
- Two more moving parts (WebLLM and transformers.js, pinned) have to be bumped deliberately; the service worker cache for
  them is named by version.
- Accuracy and speed differ per phone; the settings page links to a test that runs the server's `cases.json` on the device.
