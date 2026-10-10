namespace Kamada.Web.Shared.Api;

/// <summary>
/// Polish messages for stable API error codes. Codes without a translation fall back to the server's detail.
/// </summary>
public static class ErrorMessages
{
    private static readonly Dictionary<string, string> Messages = new(StringComparer.Ordinal)
    {
        ["Auth.InvalidCredentials"] = "Nieprawidłowy login lub hasło.",
        ["Auth.InvalidRefreshToken"] = "Sesja wygasła. Zaloguj się ponownie.",
        ["Concurrency.Conflict"] = "Dane zostały w międzyczasie zmienione przez kogoś innego. Odśwież i spróbuj ponownie.",
        ["Workers.UsernameTaken"] = "Pracownik o takim loginie już istnieje.",
        ["Workers.CannotDeactivateSelf"] = "Nie możesz dezaktywować własnego konta.",
        ["Workers.CannotChangeOwnRole"] = "Nie możesz zmienić własnej roli.",
        ["Products.SkuTaken"] = "Produkt o takim SKU już istnieje.",
        ["Products.UnknownCategory"] = "Wybrana kategoria nie istnieje.",
        ["Products.InvalidPrice"] = "Cena musi być dodatnia i mieć najwyżej 2 miejsca po przecinku.",
        ["Categories.NameTaken"] = "Na tym poziomie istnieje już kategoria o tej nazwie.",
        ["Categories.Cycle"] = "Nie można przenieść kategorii do jej własnej podkategorii.",
        ["Categories.HasChildren"] = "Najpierw usuń lub przenieś podkategorie.",
        ["Clients.UsernameTaken"] = "Klient o takim loginie już istnieje.",
        ["Clients.EmailTaken"] = "Klient o takim adresie e-mail już istnieje.",
        ["Clients.GeocodingFailed"] = "Nie udało się ustalić współrzędnych adresu. Wskaż punkt na mapie ręcznie.",
        ["Clients.Inactive"] = "Klient jest nieaktywny.",
        ["Clients.NotShippingAddress"] = "Wybierz adres dostawy (nie rozliczeniowy).",
        ["Orders.InvalidTransition"] = "Tej operacji nie można wykonać w obecnym statusie zamówienia.",
        ["Payments.AlreadyInProgress"] = "Zamówienie ma już oczekującą lub zakończoną płatność.",
        ["Payments.OrderNotAwaitingPayment"] = "Zamówienie nie oczekuje na płatność.",
        ["Payments.RefundRequiresCancelledOrder"] = "Zwrot jest możliwy tylko dla anulowanego zamówienia.",
        ["Workers.CourierNotFound"] = "Wybrany kurier nie istnieje.",
        ["Workers.NotACourier"] = "Wybrany pracownik nie jest kurierem.",
        ["Workers.CourierInactive"] = "Wybrany kurier jest nieaktywny.",
        ["Delivery.LocationNameTaken"] = "Lokalizacja o takiej nazwie już istnieje.",
        ["Delivery.LocationNotFound"] = "Wybrana lokalizacja nie istnieje.",
        ["Delivery.LocationInactive"] = "Wybrana lokalizacja jest nieaktywna.",
        ["Delivery.LocationWithoutCoordinates"] = "Lokalizacja startowa lub końcowa nie ma współrzędnych. Wskaż ją na mapie w zakładce Lokalizacje.",
        ["Delivery.RunNotFound"] = "Trasa nie istnieje.",
        ["Delivery.StopNotFound"] = "Przystanek nie należy do tej trasy.",
        ["Delivery.OrderNotInRun"] = "Zamówienie nie należy do tej trasy.",
        ["Delivery.OrderAlreadyInThisRun"] = "Zamówienie jest już na tej trasie.",
        ["Delivery.OrdersNotFound"] = "Część wybranych zamówień nie istnieje.",
        ["Delivery.OrdersNotReady"] = "Część zamówień nie jest już gotowa do wysyłki (np. trafiła na inną trasę). Odśwież listę.",
        ["Delivery.StopsWithoutCoordinates"] = "Część adresów dostawy nie ma współrzędnych. Wskaż je na mapie w karcie klienta.",
        ["Delivery.TooManyStops"] = "Trasa może mieć najwyżej 60 przystanków.",
        ["Delivery.RunHasNoStops"] = "Najpierw dodaj zamówienia do trasy.",
        ["Delivery.RouteNotPlanned"] = "Przed zatwierdzeniem zoptymalizuj trasę lub zapisz kolejność przystanków.",
        ["Delivery.InvalidSequence"] = "Kolejność musi zawierać każde zamówienie trasy dokładnie raz. Odśwież stronę.",
        ["Delivery.RunNotEditable"] = "Można zmieniać tylko trasy w wersji roboczej.",
        ["Delivery.RunNotInProgress"] = "Trasa nie jest w realizacji.",
        ["Delivery.StopAlreadyCompleted"] = "Ten przystanek został już obsłużony.",
        ["Delivery.FailureReasonRequired"] = "Podaj powód nieudanej dostawy.",
        ["Delivery.InvalidTransition"] = "Tej operacji nie można wykonać w obecnym statusie trasy.",
        ["Assistant.Disabled"] = "Asystent AI jest wyłączony.",
        ["Assistant.ModelUnavailable"] = "Model AI jest niedostępny (czy działa Ollama i jest pobrany model?). Spróbuj za chwilę.",
        ["Assistant.ExtractionFailed"] = "Asystent nie zrozumiał tekstu. Spróbuj sformułować go inaczej.",
        ["Assistant.NothingRecognized"] = "Nie rozpoznano klienta, adresu ani produktów. Uzupełnij tekst.",
        ["Assistant.SpeechToTextDisabled"] = "Dyktowanie jest wyłączone. Wpisz zamówienie ręcznie.",
        ["Assistant.SpeechToTextNotReady"] = "Model rozpoznawania mowy jest jeszcze pobierany. Spróbuj za chwilę albo wpisz tekst.",
        ["Assistant.AudioMissing"] = "Nie przesłano nagrania.",
        ["Assistant.InvalidAudio"] = "Nieobsługiwany format nagrania.",
        ["Assistant.AudioTooLarge"] = "Nagranie jest za duże.",
        ["Assistant.AudioTooLong"] = "Nagranie może trwać najwyżej 60 sekund.",
        ["Assistant.NothingHeard"] = "Nie rozpoznano mowy. Mów wyraźniej, bliżej mikrofonu.",
        ["Assistant.InvalidLanguage"] = "Nieobsługiwany język.",
    };

    public static string? TryTranslate(string code) => Messages.GetValueOrDefault(code);
}
