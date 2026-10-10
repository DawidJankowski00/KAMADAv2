namespace Kamada.Web.Shared.Audio;

/// <summary>Polish explanations for the error codes of <c>voice-recorder.js</c>.</summary>
public static class RecorderMessages
{
    public static string For(string code) => code switch
    {
        "permission" => "Brak dostępu do mikrofonu. Zezwól przeglądarce na użycie mikrofonu (ikona kłódki przy adresie strony).",
        "no-device" => "Nie znaleziono mikrofonu. Podłącz mikrofon i spróbuj ponownie.",
        "insecure" => "Mikrofon działa tylko na stronie otwartej przez HTTPS albo z adresu localhost.",
        "unsupported" => "Ta przeglądarka nie obsługuje nagrywania dźwięku. Użyj aktualnego Chrome, Edge lub Firefoksa.",
        "empty" => "Nic nie zostało nagrane.",
        "silent" => "Mikrofon nic nie zarejestrował (cisza). Sprawdź, czy nie jest wyciszony i czy przeglądarka używa właściwego mikrofonu (ikona przy adresie strony).",
        _ => "Nie udało się uruchomić nagrywania.",
    };

    /// <summary>"0:07 / 1:00".</summary>
    public static string Elapsed(TimeSpan elapsed, TimeSpan max) =>
        $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00} / {(int)max.TotalMinutes}:{max.Seconds:00}";
}
