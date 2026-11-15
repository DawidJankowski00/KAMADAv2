namespace Kamada.Modules.Assistant.Domain.Settings;

/// <summary>Who recognizes dictated notes.</summary>
internal enum SpeechProvider
{
    /// <summary>No dictation; typing still works.</summary>
    Off,

    /// <summary>Local whisper.cpp model.</summary>
    Whisper,

    /// <summary>Google Gemini API.</summary>
    Gemini,
}
