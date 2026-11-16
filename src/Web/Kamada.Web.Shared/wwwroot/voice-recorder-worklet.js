// AudioWorklet of voice-recorder.js: hands every block of microphone samples to the main thread.
// A file of its own (not a blob: URL) so the apps' Content-Security-Policy can stay at script-src 'self'.
class KamadaCapture extends AudioWorkletProcessor {
  process(inputs) {
    const channel = inputs[0] && inputs[0][0];
    if (channel) this.port.postMessage(channel.slice(0));
    return true;
  }
}

registerProcessor("kamada-capture", KamadaCapture);
