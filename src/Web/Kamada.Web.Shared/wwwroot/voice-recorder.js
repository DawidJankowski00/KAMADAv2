// Microphone capture for the VoiceRecorder Blazor component.
// Audio never leaves the browser except as the final recording sent to our own API: no Web Speech API (Chrome would
// send the audio to Google). Samples are captured with an AudioWorklet, resampled to 16 kHz mono and returned as
// 16-bit PCM; the WAV header is written in C# (WavEncoder).
const TARGET_RATE = 16000;
const LEVEL_INTERVAL_MS = 100;

const WORKLET = `
class KamadaCapture extends AudioWorkletProcessor {
  process(inputs) {
    const channel = inputs[0] && inputs[0][0];
    if (channel) this.port.postMessage(channel.slice(0));
    return true;
  }
}
registerProcessor("kamada-capture", KamadaCapture);
`;

let session = null;

/** "ok", "insecure" (mic needs HTTPS or localhost) or "unsupported". */
export function checkSupport() {
  if (!window.isSecureContext) return "insecure";
  if (!navigator.mediaDevices?.getUserMedia || !window.AudioContext || !window.AudioWorkletNode) return "unsupported";
  return "ok";
}

/** Starts recording; returns "ok" or an error code ("permission", "no-device", "insecure", "unsupported", "error"). */
export async function start(dotnet, maxSeconds) {
  const support = checkSupport();
  if (support !== "ok") return support;
  if (session) await discard();

  let stream;
  try {
    stream = await navigator.mediaDevices.getUserMedia({
      audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: true },
    });
  } catch (error) {
    if (error?.name === "NotAllowedError" || error?.name === "SecurityError") return "permission";
    if (error?.name === "NotFoundError" || error?.name === "OverconstrainedError") return "no-device";
    return "error";
  }

  try {
    const context = new AudioContext();
    const moduleUrl = URL.createObjectURL(new Blob([WORKLET], { type: "application/javascript" }));
    await context.audioWorklet.addModule(moduleUrl);
    URL.revokeObjectURL(moduleUrl);

    const source = context.createMediaStreamSource(stream);
    const node = new AudioWorkletNode(context, "kamada-capture");
    const chunks = [];
    let length = 0;
    let peak = 0;
    let lastLevel = 0;
    const maxSamples = Math.round(maxSeconds * context.sampleRate);

    node.port.onmessage = (event) => {
      if (!session) return;
      const samples = event.data;
      chunks.push(samples);
      length += samples.length;
      for (let i = 0; i < samples.length; i++) peak = Math.max(peak, Math.abs(samples[i]));

      const now = performance.now();
      if (now - lastLevel >= LEVEL_INTERVAL_MS) {
        lastLevel = now;
        const level = Math.min(1, peak * 1.5);
        peak = 0;
        dotnet.invokeMethodAsync("OnLevel", level, length / context.sampleRate).catch(() => {});
      }
      if (length >= maxSamples) {
        dotnet.invokeMethodAsync("OnLimitReached").catch(() => {});
      }
    };

    source.connect(node);
    // Keep the graph running without playing the microphone back.
    const mute = context.createGain();
    mute.gain.value = 0;
    node.connect(mute).connect(context.destination);

    session = { stream, context, source, node, chunks, get length() { return length; }, maxSamples };
    return "ok";
  } catch {
    stream.getTracks().forEach((t) => t.stop());
    return "error";
  }
}

/** Stops recording and returns 16 kHz mono 16-bit little-endian PCM samples (null when nothing was recorded). */
export async function stop() {
  if (!session) return null;
  const { chunks, context, maxSamples } = session;
  const length = Math.min(session.length, maxSamples);
  const sourceRate = context.sampleRate;
  await discard();
  if (length === 0) return null;

  const merged = new Float32Array(length);
  let offset = 0;
  for (const chunk of chunks) {
    const take = Math.min(chunk.length, length - offset);
    merged.set(chunk.subarray(0, take), offset);
    offset += take;
    if (offset >= length) break;
  }

  const resampled = await resample(merged, sourceRate);
  const pcm = new Int16Array(resampled.length);
  for (let i = 0; i < resampled.length; i++) {
    const s = Math.max(-1, Math.min(1, resampled[i]));
    pcm[i] = s < 0 ? s * 0x8000 : s * 0x7fff;
  }
  return new Uint8Array(pcm.buffer);
}

/** Stops without returning anything (cancel, component disposed). */
export async function discard() {
  if (!session) return;
  const { stream, context, source, node } = session;
  session = null;
  node.port.onmessage = null;
  try { source.disconnect(); node.disconnect(); } catch { /* already disconnected */ }
  stream.getTracks().forEach((t) => t.stop());
  await context.close().catch(() => {});
}

/** Browser-quality resampling (with anti-aliasing) through an OfflineAudioContext. */
async function resample(samples, sourceRate) {
  if (sourceRate === TARGET_RATE) return samples;
  const frames = Math.max(1, Math.round(samples.length * TARGET_RATE / sourceRate));
  const offline = new OfflineAudioContext(1, frames, TARGET_RATE);
  const buffer = offline.createBuffer(1, samples.length, sourceRate);
  buffer.copyToChannel(samples, 0);
  const player = offline.createBufferSource();
  player.buffer = buffer;
  player.connect(offline.destination);
  player.start();
  const rendered = await offline.startRendering();
  return rendered.getChannelData(0);
}
