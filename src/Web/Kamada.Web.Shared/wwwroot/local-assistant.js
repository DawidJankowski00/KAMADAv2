// On-device order assistant: a language model (WebLLM, WebGPU) and Whisper (transformers.js). The libraries and the model
// files come from our own server (deploy/assistant/*.sh, served under /models), never from a third-party CDN, and
// everything runs on the phone: nothing said or typed is sent anywhere. Weights are kept in Cache Storage.
//
// Models are described by the C# side: { id, kind: "llm" | "stt", ... }. Progress goes back through a .NET callback object.

const state = {
    config: null,
    webllm: null,
    engine: null,
    llmId: null,
    transformers: null,
    asr: null,
    sttId: null,
    sttDevice: null,
    lastAnswer: null,
    lastFinish: null,
};

/** @param {{ vendorUrl: string, modelsUrl: string }} config base URLs ending with "/"; relative ones are resolved against the page */
export function configure(config) {
    const resolve = url => new URL(url, document.baseURI).href;
    state.config = { vendorUrl: resolve(config.vendorUrl), modelsUrl: resolve(config.modelsUrl) };
}

/** What this device can run: WebGPU (and 16-bit shaders, which the q4f16 models need), memory, storage quota. */
export async function capabilities() {
    const result = {
        webGpu: false,
        shaderF16: false,
        adapter: null,
        deviceMemoryGb: navigator.deviceMemory ?? null,
        cores: navigator.hardwareConcurrency ?? null,
        storageQuotaBytes: null,
        storageUsedBytes: null,
        persisted: null,
    };
    try {
        const adapter = navigator.gpu ? await navigator.gpu.requestAdapter() : null;
        if (adapter) {
            result.webGpu = true;
            result.shaderF16 = adapter.features.has("shader-f16");
            const info = adapter.info ?? {};
            result.adapter = [info.vendor, info.architecture].filter(Boolean).join(" ") || null;
        }
    } catch {
        // No usable adapter.
    }
    try {
        const estimate = await navigator.storage?.estimate?.();
        result.storageQuotaBytes = estimate?.quota ?? null;
        result.storageUsedBytes = estimate?.usage ?? null;
        result.persisted = (await navigator.storage?.persisted?.()) ?? null;
    } catch {
        // Storage estimate is optional.
    }
    return result;
}

/** Asks the browser not to evict the downloaded models under storage pressure. */
export async function persist() {
    try {
        return (await navigator.storage?.persist?.()) ?? false;
    } catch {
        return false;
    }
}

// ---------------------------------------------------------------------------------------------------------------
// Language model

async function webllm() {
    state.webllm ??= await import(state.config.vendorUrl + "web-llm.js");
    return state.webllm;
}

function llmAppConfig(model) {
    const base = state.config.modelsUrl + `llm/${model.id}/`;
    return {
        useIndexedDBCache: false,
        model_list: [
            {
                model: base,
                model_id: model.id,
                model_lib: base + "lib.wasm",
                overrides: { context_window_size: model.contextWindow },
            },
        ],
    };
}

/** Whether the model's weights are already in Cache Storage (no download needed). */
export async function isLlmCached(model) {
    const lib = await webllm();
    return lib.hasModelInCache(model.id, llmAppConfig(model));
}

/** Downloads (first time) and loads the model into GPU memory. */
export async function loadLlm(model, progress) {
    if (state.llmId === model.id && state.engine) {
        return;
    }
    await unloadLlm();
    const lib = await webllm();
    state.engine = await lib.CreateMLCEngine(model.id, {
        appConfig: llmAppConfig(model),
        initProgressCallback: report => progress?.invokeMethodAsync("Report", report.progress, report.text),
    });
    state.llmId = model.id;
}

export async function unloadLlm() {
    if (state.engine) {
        await state.engine.unload();
    }
    state.engine = null;
    state.llmId = null;
}

export function loadedLlm() {
    return state.llmId;
}

export async function deleteLlm(model) {
    if (state.llmId === model.id) {
        await unloadLlm();
    }
    const lib = await webllm();
    await lib.deleteModelAllInfoInCache(model.id, llmAppConfig(model));
}

/**
 * One extraction: chat messages in, the model's JSON answer out. Greedy decoding and a grammar built from the JSON schema,
 * so the answer always has the shape the server's assistant expects (same prompt, same schema).
 */
export async function extract(messages, schema) {
    if (!state.engine) {
        throw new Error("The language model is not loaded.");
    }
    try {
        const reply = await state.engine.chat.completions.create({
            messages,
            temperature: 0,
            max_tokens: 800,
            response_format: { type: "json_object", schema },
            // Qwen 3 reasons before answering unless told not to: hundreds of tokens we do not need.
            ...(state.llmId?.startsWith("Qwen3") ? { extra_body: { enable_thinking: false } } : {}),
        });
        state.lastAnswer = reply.choices[0]?.message?.content ?? "";
        state.lastFinish = reply.choices[0]?.finish_reason ?? null;
        return state.lastAnswer;
    } catch (error) {
        // A lost GPU device (memory pressure, driver reset) leaves the engine unusable: drop it so the next call reloads it.
        state.engine = null;
        state.llmId = null;
        throw error;
    }
}

// ---------------------------------------------------------------------------------------------------------------
// Speech recognition

async function transformers() {
    if (!state.transformers) {
        const lib = await import(state.config.vendorUrl + "transformers.js");
        lib.env.allowLocalModels = false;
        lib.env.allowRemoteModels = true;
        lib.env.remoteHost = state.config.modelsUrl + "stt/";
        lib.env.remotePathTemplate = "{model}/resolve/{revision}/";
        lib.env.useBrowserCache = true;
        lib.env.backends.onnx.wasm.wasmPaths = state.config.vendorUrl + "ort/";
        state.transformers = lib;
    }
    return state.transformers;
}

const sttFiles = {
    webgpu: { encoder_model: "fp16", decoder_model_merged: "q4" },
    wasm: { encoder_model: "q8", decoder_model_merged: "q8" },
};

function sttCacheKeyPrefix(model) {
    return `${state.config.modelsUrl}stt/${model.repo}/`;
}

/** Whether the speech model's files are in the browser cache (transformers.js keeps them in the "transformers-cache" cache). */
export async function isSttCached(model) {
    if (!("caches" in self)) {
        return false;
    }
    const cache = await caches.open("transformers-cache");
    const keys = await cache.keys();
    return keys.some(request => request.url.startsWith(sttCacheKeyPrefix(model)) && request.url.endsWith(".onnx"));
}

export async function loadStt(model, progress) {
    if (state.sttId === model.id && state.asr) {
        return;
    }
    await unloadStt();
    const lib = await transformers();
    const caps = await capabilities();
    const device = caps.webGpu && caps.shaderF16 ? "webgpu" : "wasm";
    // transformers.js reports per file; the sum of the bytes of all files gives one overall fraction.
    const files = new Map();
    state.asr = await lib.pipeline("automatic-speech-recognition", model.repo, {
        device,
        dtype: sttFiles[device],
        progress_callback: item => {
            if (item.status === "progress" && item.total) {
                files.set(item.file, { loaded: item.loaded, total: item.total });
                let loaded = 0;
                let total = 0;
                for (const file of files.values()) {
                    loaded += file.loaded;
                    total += file.total;
                }
                progress?.invokeMethodAsync("Report", loaded / total, `Pobieranie ${item.file}`);
            }
        },
    });
    state.sttId = model.id;
    state.sttDevice = device;
}

export async function unloadStt() {
    if (state.asr) {
        await state.asr.dispose?.();
    }
    state.asr = null;
    state.sttId = null;
}

export function loadedStt() {
    return state.sttId ? { id: state.sttId, device: state.sttDevice } : null;
}

export async function deleteStt(model) {
    if (state.sttId === model.id) {
        await unloadStt();
    }
    if ("caches" in self) {
        const cache = await caches.open("transformers-cache");
        for (const request of await cache.keys()) {
            if (request.url.startsWith(sttCacheKeyPrefix(model))) {
                await cache.delete(request);
            }
        }
    }
}

/** @param wav 16 kHz mono 16-bit PCM WAV (what VoiceRecorder produces), @param language "pl" | "en" | "auto" */
export async function transcribe(wav, language) {
    if (!state.asr) {
        throw new Error("The speech model is not loaded.");
    }
    const bytes = wav instanceof Uint8Array ? wav : new Uint8Array(wav);
    const samples = new Float32Array((bytes.length - 44) / 2);
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    for (let i = 0; i < samples.length; i++) {
        samples[i] = view.getInt16(44 + i * 2, true) / 32768;
    }
    // Bounded and without repeats: Whisper hallucinates loops on unclear audio.
    const options = { task: "transcribe", chunk_length_s: 30, max_new_tokens: 256, no_repeat_ngram_size: 4 };
    if (language === "pl") {
        options.language = "polish";
    } else if (language === "en") {
        options.language = "english";
    }
    const output = await state.asr(samples, options);
    return (Array.isArray(output) ? output[0] : output).text.trim();
}

/** Frees everything held in memory (not the downloaded files). */
export async function unloadAll() {
    await unloadLlm();
    await unloadStt();
}

/** The raw answer of the last extraction and why generation stopped – for the accuracy page and debugging. */
export function lastExtraction() {
    return { answer: state.lastAnswer, finish: state.lastFinish };
}
