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
    };

    public static string? TryTranslate(string code) => Messages.GetValueOrDefault(code);
}
