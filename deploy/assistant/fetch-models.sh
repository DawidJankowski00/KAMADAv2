#!/bin/sh
# Downloads on-device assistant models into a directory served by nginx under /models:
#   <out>/llm/<id>/resolve/main/...   language models in MLC format + <out>/llm/<id>/lib.wasm (WebGPU kernels)
#   <out>/stt/<repo>/resolve/main/... Whisper in ONNX format
# so the phone fetches them from our own server, never from a third-party CDN. Already downloaded files are kept.
#   usage: fetch-models.sh <out> [model ...]    (default: the models in $ASSISTANT_MODELS)
# Needs curl and sed. The catalogue below lists what the settings page can offer.
set -eu

OUT="$1"
shift
MODELS="${*:-${ASSISTANT_MODELS:-}}"
HF="${ASSISTANT_HF_URL:-https://huggingface.co}"
LIBS="${ASSISTANT_MLC_LIBS_URL:-https://raw.githubusercontent.com/mlc-ai/binary-mlc-llm-libs/main/web-llm-models/v0_2_86/base}"

get() { # url target [optional]
    [ -s "$2" ] && return 0
    mkdir -p "$(dirname "$2")"
    echo "  $2"
    if curl -fsL --retry 3 -o "$2.part" "$1"; then mv "$2.part" "$2"; elif [ -n "${3:-}" ]; then rm -f "$2.part"; else exit 1; fi
}

llm() { # id lib-file
    dir="$OUT/llm/$1"
    echo "Language model $1"
    get "$LIBS/$2" "$dir/lib.wasm"
    for file in mlc-chat-config.json tensor-cache.json tokenizer.json; do
        get "$HF/mlc-ai/$1/resolve/main/$file" "$dir/resolve/main/$file"
    done
    for file in ndarray-cache.json tokenizer_config.json vocab.json merges.txt tokenizer.model; do
        get "$HF/mlc-ai/$1/resolve/main/$file" "$dir/resolve/main/$file" optional
    done
    for shard in $(sed -n 's/.*"dataPath": *"\([^"]*\)".*/\1/p' "$dir/resolve/main/tensor-cache.json"); do
        get "$HF/mlc-ai/$1/resolve/main/$shard" "$dir/resolve/main/$shard"
    done
}

stt() { # repo
    dir="$OUT/stt/$1/resolve/main"
    echo "Speech model $1"
    for file in config.json generation_config.json preprocessor_config.json tokenizer.json tokenizer_config.json \
        added_tokens.json merges.txt normalizer.json special_tokens_map.json vocab.json; do
        get "$HF/$1/resolve/main/$file" "$dir/$file"
    done
    # WebGPU: fp16 encoder + 4-bit decoder; WebAssembly (no GPU): 8-bit quantized.
    for file in encoder_model_fp16 decoder_model_merged_q4 encoder_model_quantized decoder_model_merged_quantized; do
        get "$HF/$1/resolve/main/onnx/$file.onnx" "$dir/onnx/$file.onnx"
    done
}

for model in $MODELS; do
    case "$model" in
        Qwen2.5-0.5B-Instruct-q4f16_1-MLC) llm "$model" Qwen2-0.5B-Instruct-q4f16_1_cs1k-webgpu.wasm ;;
        Qwen2.5-1.5B-Instruct-q4f16_1-MLC) llm "$model" Qwen2-1.5B-Instruct-q4f16_1_cs1k-webgpu.wasm ;;
        Llama-3.2-1B-Instruct-q4f16_1-MLC) llm "$model" Llama-3.2-1B-Instruct-q4f16_1_cs1k-webgpu.wasm ;;
        gemma3-1b-it-q4f16_1-MLC) llm "$model" gemma3-1b-it-q4f16_1_cs1k-webgpu.wasm ;;
        SmolLM2-1.7B-Instruct-q4f16_1-MLC) llm "$model" SmolLM2-1.7B-Instruct-q4f16_1_cs1k-webgpu.wasm ;;
        onnx-community/whisper-tiny | onnx-community/whisper-base | onnx-community/whisper-small) stt "$model" ;;
        *) echo "Unknown model: $model" >&2; exit 1 ;;
    esac
done
echo "Models ready in $OUT"
