using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Assistant.Application;

internal static class AssistantErrors
{
    public static readonly Error Disabled = Error.Unavailable(
        "Assistant.Disabled", "The order assistant is switched off (Assistant:Enabled).");

    public static readonly Error ModelUnavailable = Error.Unavailable(
        "Assistant.ModelUnavailable", "The local language model is not reachable or not loaded. Try again in a moment.");

    public static readonly Error ExtractionFailed = Error.Validation(
        "Assistant.ExtractionFailed", "The language model returned an answer that is not a valid order.");

    public static readonly Error NothingRecognized = Error.Validation(
        "Assistant.NothingRecognized", "No client, address or product was recognized in the text.");

    public static readonly Error SpeechToTextDisabled = Error.Unavailable(
        "Assistant.SpeechToTextDisabled", "Dictation is switched off (Assistant:Whisper:Enabled).");

    public static readonly Error SpeechToTextNotReady = Error.Unavailable(
        "Assistant.SpeechToTextNotReady", "The speech model is still being downloaded or loaded. Try again in a moment.");

    public static readonly Error AudioMissing = Error.Validation(
        "Assistant.AudioMissing", "Send the recording as the multipart field 'audio'.");

    public static readonly Error InvalidAudio = Error.Validation(
        "Assistant.InvalidAudio", "The recording must be a 16 kHz mono 16-bit PCM WAV file.");

    public static readonly Error InvalidLanguage = Error.Validation(
        "Assistant.InvalidLanguage", "Language must be one of: auto, pl, en.");

    public static readonly Error NothingHeard = Error.Validation(
        "Assistant.NothingHeard", "No speech was recognized in the recording.");

    public static readonly Error SilentRecording = Error.Validation(
        "Assistant.SilentRecording", "The recording is silent – check that the microphone is on and selected.");

    public static Error AudioTooLarge(int maxBytes) => Error.Validation(
        "Assistant.AudioTooLarge", $"The recording can have at most {maxBytes / 1024} kB.");

    public static Error AudioTooLong(TimeSpan max) => Error.Validation(
        "Assistant.AudioTooLong", $"The recording can be at most {max.TotalSeconds:0} seconds long.");
}
