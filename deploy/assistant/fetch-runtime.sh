#!/bin/sh
# Downloads the browser libraries of the on-device assistant into a directory (default: the phone app's
# wwwroot/assistant/vendor). Versions are pinned, npm tarballs are verified against their published sha512.
#   WebLLM            language model on WebGPU
#   transformers.js   Whisper speech recognition (ONNX Runtime Web inside)
# Needs curl, tar and openssl. The files are not committed (.gitignore); the Docker build runs this script.
set -eu

OUT="${1:-$(dirname "$0")/../../src/Web/Kamada.MobileApp/wwwroot/assistant/vendor}"
WEBLLM_VERSION=0.2.86
WEBLLM_SHA512="plciXBCYtY7iTKHzEcsqOfSO+s5u8NqMYFPnHmRbsN8CEdKcolgXIoGLPbzO6FtJHV43m7YP00x6qQ6Kb8CJJw=="
TRANSFORMERS_VERSION=4.3.1
TRANSFORMERS_SHA512="5+voSeUCFK6PCKlY15dPhNOKq3/zOTxDN7oC+Wy9dFb1QZ0TSQomZRRy7URRK8YRuUoE9fGMQTTPC5d/pWBJhA=="
ORT_VERSION=1.31.0-dev.20260914-8d85527a0   # the ONNX Runtime Web build transformers.js 4.3.1 is released with

mkdir -p "$OUT/ort"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

fetch_package() { # name tarball-url sha512(base64)
    curl -fsSL "$2" -o "$WORK/$1.tgz"
    actual="$(openssl dgst -sha512 -binary "$WORK/$1.tgz" | openssl base64 -A)"
    [ "$actual" = "$3" ] || { echo "Integrity check failed for $1" >&2; exit 1; }
    mkdir -p "$WORK/$1" && tar xzf "$WORK/$1.tgz" -C "$WORK/$1"
}

fetch_package web-llm "https://registry.npmjs.org/@mlc-ai/web-llm/-/web-llm-$WEBLLM_VERSION.tgz" "$WEBLLM_SHA512"
cp "$WORK/web-llm/package/lib/index.js" "$OUT/web-llm.js"
cp "$WORK/web-llm/package/LICENSE" "$OUT/web-llm.LICENSE"

fetch_package transformers "https://registry.npmjs.org/@huggingface/transformers/-/transformers-$TRANSFORMERS_VERSION.tgz" "$TRANSFORMERS_SHA512"
cp "$WORK/transformers/package/dist/transformers.min.js" "$OUT/transformers.js"
cp "$WORK/transformers/package/LICENSE" "$OUT/transformers.LICENSE"

# ONNX Runtime Web: the WebAssembly files transformers.js loads next to its own script.
ORT_BASE="https://cdn.jsdelivr.net/npm/onnxruntime-web@$ORT_VERSION/dist"
for file in ort-wasm-simd-threaded.asyncify.mjs ort-wasm-simd-threaded.asyncify.wasm ort-wasm-simd-threaded.jsep.mjs ort-wasm-simd-threaded.jsep.wasm; do
    curl -fsSL "$ORT_BASE/$file" -o "$OUT/ort/$file"
done

echo "Assistant runtime ready in $OUT"
