namespace Kamada.Web.Shared.Audio;

/// <summary>Polish explanations for the error codes of an <c>IAudioRecorder</c> (browser or native).</summary>
public static class RecorderMessages
{
    public static string For(string code) => code switch
    {
        "permission" => "Brak dostępu do mikrofonu. Zezwól przeglądarce na użycie mikrofonu (ikona kłódki przy adresie strony).",
        "permission-app" => "Brak dostępu do mikrofonu. Zezwól aplikacji KAMADA na mikrofon w ustawieniach telefonu (Aplikacje → KAMADA → Uprawnienia).",
        "no-device" => "Nie znaleziono mikrofonu. Podłącz mikrofon i spróbuj ponownie.",
        "insecure" => "Mikrofon działa tylko na stronie otwartej przez HTTPS albo z adresu localhost.",
        "unsupported" => "Ta przeglądarka nie obsługuje nagrywania dźwięku. Użyj aktualnego Chrome, Edge lub Firefoksa.",
        "empty" => "Nic nie zostało nagrane.",
        "silent" => "Mikrofon nic nie zarejestrował (cisza). Sprawdź, czy nie jest wyciszony i czy używany jest właściwy mikrofon.",
        _ => "Nie udało się uruchomić nagrywania.",
    };

    /// <summary>"0:07 / 1:00".</summary>
    public static string Elapsed(TimeSpan elapsed, TimeSpan max) =>
        $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00} / {(int)max.TotalMinutes}:{max.Seconds:00}";
}
