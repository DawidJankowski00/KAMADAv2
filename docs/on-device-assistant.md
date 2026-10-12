# On-device order assistant: measurements and how to take them

Companion of [ADR 0010](adr/0010-on-device-assistant-as-a-fallback.md). The server assistant has its model table in the
README; this page holds the same measurements for the models that run **in the phone's browser**, and the exact steps to
repeat them on any phone. Nothing here is guessed: every number comes from the accuracy page of the app
(*Asystent w telefonie → Sprawdź dokładność modelu na tym telefonie*), which runs the server evals' 40 notes
(`benchmarks/Kamada.Assistant.Evals/cases.json`, 26 Polish and 14 English) through the model on the device, with the same
scoring rules, and copies the result as Markdown.

## What runs

| Part | Library (pinned) | Runs on | Weights |
|---|---|---|---|
| Language model | WebLLM 0.2.86 (MLC format, JSON-schema grammar, temperature 0) | WebGPU with 16-bit shaders | Cache Storage, from our `/models` |
| Speech recognition | transformers.js 4.3.1 + ONNX Runtime Web 1.31.0-dev.20260914-8d85527a0 | WebGPU (fp16 encoder, 4-bit decoder), otherwise WebAssembly (8-bit) | same |
| Matching | `Kamada.Shared.Matching` (C#, WebAssembly) | CPU | – |

Pinned revisions of every model and of the WebGPU kernels are in `deploy/assistant/fetch-models.sh`; libraries and their
checksums in `deploy/assistant/fetch-runtime.sh`.

## Candidates

Sizes are what the phone downloads. Licences are the upstream ones (check the model card before shipping commercially).

| Model | Download | Context used | Licence | Note |
|---|---|---|---|---|
| Qwen 3 1.7B (q4f16_1) | 945 MB | 4096 | Apache-2.0 | **default**; thinking switched off |
| Qwen 3 0.6B (q4f16_1) | 340 MB | 4096 | Apache-2.0 | **lite** |
| Qwen 2.5 1.5B Instruct (q4f16_1) | 850 MB | 4096 | Apache-2.0 | offered |
| Qwen 2.5 0.5B Instruct (q4f16_1) | 285 MB | 4096 | Apache-2.0 | dropped (3% fully correct) |
| Llama 3.2 1B Instruct (q4f16_1) | 680 MB | 4096 | Llama 3.2 Community License | dropped (18%, weak Polish) |
| Whisper tiny / base / small (ONNX) | 105 / 165 / 410 MB (WebGPU set) | – | MIT | base default, tiny lite |

Tried and dropped: **Gemma 3 1B IT**. With the MLC weights of 2026-10 and WebLLM 0.2.86 it does not load with the context window
WebLLM's own model list asks for (`context_window_size` and the config's `sliding_window_size` conflict). Forcing full attention
(`sliding_window_size: -1`) loads and answers a short prompt, but with the extraction prompt it produced garbage (`"name": "Kata"`,
then whitespace until the token limit, 50 s per note), so it is not offered.
Not tried: models above ~2 GB of GPU memory (they do not fit most phones), and Bielik / gemma3:4b, which are the server's
models.

## Taking the measurements

A browser only offers WebGPU on a **secure origin**, so `http://<LAN address>:5082` does not work on a phone (the page
would say WebGPU is missing). Two ways that do:

1. **USB debugging (Android, Chrome).** Enable USB debugging, connect the phone and forward the three ports so the phone's
   `localhost` is this computer:

   ```bash
   adb reverse tcp:5080 tcp:5080   # API
   adb reverse tcp:5082 tcp:5082   # phone app
   adb reverse tcp:5083 tcp:5083   # model files (development setting in appsettings.Development.json)
   ```

   Then open `http://localhost:5082` on the phone.
2. **HTTPS** in front of the stack (any reverse proxy or tunnel with a certificate); set `PUBLIC_API_URL` accordingly and
   serve the models from the app's own `/models` (`docker compose --profile assistant up assistant-models`).

Steps:

```bash
sh deploy/assistant/fetch-runtime.sh                                   # libraries (once)
sh deploy/assistant/fetch-models.sh <dir> Qwen3-1.7B-q4f16_1-MLC Qwen3-0.6B-q4f16_1-MLC Qwen2.5-1.5B-Instruct-q4f16_1-MLC \
    onnx-community/whisper-tiny onnx-community/whisper-base
docker compose down -v && docker compose up -d                          # API with the current demo data (see the note below)
dotnet run --project src/Web/Kamada.MobileApp --launch-profile http    # phone app (development)
docker run --rm -p 5083:80 -v <dir>:/srv/models:ro \
    -v "$PWD/deploy/assistant/dev-models.conf":/etc/nginx/conf.d/default.conf:ro nginx:1.29-alpine   # model files (CORS open)
```

The cases refer to the current demo catalogue (the "Napoje" and "Pieczywo" categories): a database seeded before they were added
fails every note with water or bread whichever model runs, so start from a reset volume (`down -v` deletes the local data).

On the phone: sign in as `sprzedawca1`, open *Asystent w telefonie*, download a language model and a speech model, switch
the assistant to the phone, open the accuracy page and run it. Record, per model: download time, **time to load** (first
draft after the page was opened), **time per dictation** (record a 10 s order with *Dyktuj*, read the time until the text
appears), the accuracy table and whether the page stayed stable (a *Device was lost* error means the GPU ran out of memory).

## Results

### Desktop GPU – for comparison with the server table

Measured 2026-10-11 with the accuracy page, one run per model, 40 notes, the demo database freshly seeded, geocoding off
(addresses are never completed on the phone). The GPU is an AMD RDNA 3 with 32 GB of shared memory in a laptop, so it
**is not a phone**: it shows how the models compare with each other and with the server, not how fast a phone will be.
Latency is per draft with the model already loaded; the first draft after opening the app also compiles the WebGPU
shaders and loads the weights (informally 40–75 s here, not included).

| Model | Client decision | Address decision | Product | Quantity | All correct | p50 / p95 |
|---|---|---|---|---|---|---|
| `Qwen3-1.7B` (thinking off) | 95% (38/40) | 68% (27/40) | 68% (40/59) | 90% (53/59) | **50%** (20/40) | 9.7 s / 10.7 s |
| `Qwen3-0.6B` (thinking off) | 88% (35/40) | 38% (15/40) | 40% (22/55) | 44% (24/55) | 13% (5/40) | 6.0 s / 6.9 s |
| `Qwen2.5-1.5B-Instruct` | 95% (38/40) | 70% (28/40) | 62% (36/58) | 90% (52/58) | 33% (13/40) | 10.1 s / 11.5 s |
| `Llama-3.2-1B-Instruct` | 85% (34/40) | 35% (14/40) | 51% (29/57) | 84% (48/57) | 18% (7/40) | 5.8 s / 7.6 s |
| `Qwen2.5-0.5B-Instruct` | 88% (35/40) | 20% (8/40) | 25% (18/73) | 58% (42/73) | 3% (1/40) | 4.7 s / 5.6 s |
| for comparison: server `gemma3:4b` (README) | 98% | 95% | 87% | 100% | 78% | 0.8 s GPU / 13.7 s CPU |

- **Qwen 3 1.7B is the best model that fits a phone**: half of the notes come out fully right (about 70% of the first 23
  Polish ones) and the client decision is as good as the server's neighbour models. Its misses are what small models do: genitive
  streets and number words ("Kartuską", "pięćset"), products translated badly from English, a wrong auto-selection in the
  English notes. A draft is still only a suggestion the seller checks; the server stays the default for that reason.
- **Qwen 3 0.6B is the "lite" model**: usable for the client and often for a simple note, but only 13% fully right.
  Qwen 2.5 0.5B (3%) and Llama 3.2 1B (18%, weak Polish) are not offered. Gemma 3 1B does not run (see above).
- Two fixes came out of the first runs and apply to the server too: house or apartment numbers the model puts in the wrong
  address field are moved where they belong (Qwen 2.5 1.5B: address decision 53% → 70%), and a leading `<think>…</think>`
  block is ignored (Qwen 3 wrote an empty one before every answer).
- Speech recognition was loaded and run (Whisper tiny, WebGPU, ~4 s to load from the cache) but its accuracy on a real voice
  is **not** measured: it needs a person dictating. Do it on the phone: dictate the notes of `cases.json` and note what
  *Dyktuj* returns.

### Phone

Not measured yet. Paste the Markdown copied from the accuracy page here, one row per model and phone, together with the
phone model, Android and Chrome versions.

| Phone | Model | Load | Time per draft (p50 / p95) | Client | Address | Product | Quantity | All correct |
|---|---|---|---|---|---|---|---|---|
| _to be measured_ | | | | | | | | |

## Choosing the defaults

`LocalModels` lists the models the settings page offers; the first entry of each kind is the default, the second is the
"lite" one. The current choice (Qwen 3 1.7B with Whisper base, lite: Qwen 3 0.6B with Whisper tiny) comes from the desktop
table; **confirm it on the phone** and change the order in `LocalModels` if the phone says otherwise. The rule: the default is
the model with the highest share of fully correct Polish notes that loads in under a minute and keeps the p95 under ~30 s on
the target phone; "lite" is the smallest model that is still usable (a draft the seller can fix is fine, a wrong
auto-selection is not).
