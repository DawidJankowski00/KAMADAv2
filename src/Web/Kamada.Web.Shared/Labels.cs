using System.Globalization;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Shared.Contracts.Payments;
using Kamada.Shared.Contracts.Workforce;
using MudBlazor;

namespace Kamada.Web.Shared;

/// <summary>Polish UI labels and formatting for values coming from the API.</summary>
public static class Labels
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static string Money(decimal amount) => amount.ToString("N2", Polish) + " zł";

    public static string DateTime(DateTimeOffset value) => value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", Polish);

    public static string Date(DateTimeOffset value) => value.ToLocalTime().ToString("dd.MM.yyyy", Polish);

    /// <summary>"53.12345, 18.01234" – invariant (dot) decimals so the comma separates the two numbers.</summary>
    public static string Coordinates(double latitude, double longitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{latitude:F5}, {longitude:F5}");

    public static string Of(OrderStatus status) => status switch
    {
        OrderStatus.New => "Nowe",
        OrderStatus.Paid => "Opłacone",
        OrderStatus.ReadyForDelivery => "Gotowe do wysyłki",
        OrderStatus.AssignedToRun => "Przypisane do trasy",
        OrderStatus.OutForDelivery => "W doręczeniu",
        OrderStatus.Delivered => "Dostarczone",
        OrderStatus.DeliveryFailed => "Nieudana dostawa",
        OrderStatus.Cancelled => "Anulowane",
        _ => status.ToString(),
    };

    public static Color ColorOf(OrderStatus status) => status switch
    {
        OrderStatus.New => Color.Default,
        OrderStatus.Paid => Color.Info,
        OrderStatus.ReadyForDelivery => Color.Primary,
        OrderStatus.AssignedToRun => Color.Secondary,
        OrderStatus.OutForDelivery => Color.Warning,
        OrderStatus.Delivered => Color.Success,
        OrderStatus.DeliveryFailed => Color.Error,
        OrderStatus.Cancelled => Color.Dark,
        _ => Color.Default,
    };

    public static string Of(PaymentProvider provider) => provider switch
    {
        PaymentProvider.Cash => "Gotówka",
        PaymentProvider.Card => "Karta",
        PaymentProvider.Transfer => "Przelew",
        _ => provider.ToString(),
    };

    public static string Of(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Oczekuje",
        PaymentStatus.Completed => "Zakończona",
        PaymentStatus.Failed => "Odrzucona",
        PaymentStatus.Refunded => "Zwrócona",
        _ => status.ToString(),
    };

    public static Color ColorOf(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => Color.Warning,
        PaymentStatus.Completed => Color.Success,
        PaymentStatus.Failed => Color.Error,
        _ => Color.Default,
    };

    public static string Of(AddressType type) => type == AddressType.Shipping ? "Dostawy" : "Rozliczeniowy";

    public static string Of(WorkerRole role) => role == WorkerRole.Admin ? "Administrator" : "Kurier";

    /// <summary>Polish name of a role claim value; unknown roles are shown as they are.</summary>
    public static string Role(string role) => Enum.GetNames<WorkerRole>().Contains(role) ? Of(Enum.Parse<WorkerRole>(role)) : role;

    public static string Format(AddressDto a) =>
        $"{a.Street} {a.BuildingNumber}{(a.ApartmentNumber is null ? string.Empty : "/" + a.ApartmentNumber)}, {a.PostalCode} {a.City}";

    public static string Format(ShippingAddressDto a) =>
        $"{a.Street} {a.BuildingNumber}{(a.ApartmentNumber is null ? string.Empty : "/" + a.ApartmentNumber)}, {a.PostalCode} {a.City}";

    public static string Date(DateOnly value) => value.ToString("dd.MM.yyyy", Polish);

    /// <summary>"14:05" in the device's time zone.</summary>
    public static string Time(DateTimeOffset value) => value.ToLocalTime().ToString("HH:mm", Polish);

    /// <summary>
    /// Estimated arrival at a stop: a clock time once the run has started ("ok. 14:05"), otherwise the offset
    /// from the start ("45 min od startu"). <c>null</c> when the run has no plan.
    /// </summary>
    public static string? Arrival(DateTimeOffset? startedAt, double? offsetSeconds) =>
        offsetSeconds is not { } offset ? null
        : startedAt is { } start ? "ok. " + Time(start.AddSeconds(offset))
        : Duration(offset) + " od startu";

    /// <summary>"1 przystanek", "3 przystanki", "5 przystanków", "22 przystanki" (Polish plural forms).</summary>
    public static string StopCount(int count)
    {
        var word = count == 1 ? "przystanek"
            : count % 10 is >= 2 and <= 4 && count % 100 is not (>= 12 and <= 14) ? "przystanki"
            : "przystanków";
        return $"{count} {word}";
    }

    /// <summary><c>tel:</c> link for a phone number typed with spaces, dashes or brackets ("+48 601-111 111" → "tel:+48601111111").</summary>
    public static string PhoneUri(string phoneNumber) =>
        "tel:" + (phoneNumber.TrimStart().StartsWith('+') ? "+" : string.Empty) + string.Concat(phoneNumber.Where(char.IsAsciiDigit));

    /// <summary>"45 min", "1 h 05 min".</summary>
    public static string Duration(double seconds)
    {
        var minutes = (int)Math.Round(seconds / 60, MidpointRounding.AwayFromZero);
        return minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h {minutes % 60:00} min";
    }

    /// <summary>"850 m", "19,0 km".</summary>
    public static string Distance(double meters) =>
        meters < 1000 ? $"{Math.Round(meters):0} m" : (meters / 1000).ToString("0.0", Polish) + " km";

    public static string Of(DeliveryRunStatus status) => status switch
    {
        DeliveryRunStatus.Draft => "Robocza",
        DeliveryRunStatus.Planned => "Zaplanowana",
        DeliveryRunStatus.InProgress => "W realizacji",
        DeliveryRunStatus.Completed => "Zakończona",
        DeliveryRunStatus.Cancelled => "Anulowana",
        _ => status.ToString(),
    };

    public static Color ColorOf(DeliveryRunStatus status) => status switch
    {
        DeliveryRunStatus.Draft => Color.Default,
        DeliveryRunStatus.Planned => Color.Primary,
        DeliveryRunStatus.InProgress => Color.Warning,
        DeliveryRunStatus.Completed => Color.Success,
        DeliveryRunStatus.Cancelled => Color.Dark,
        _ => Color.Default,
    };

    public static string Of(DeliveryStopStatus status) => status switch
    {
        DeliveryStopStatus.Pending => "Oczekuje",
        DeliveryStopStatus.Delivered => "Dostarczone",
        DeliveryStopStatus.Failed => "Nieudane",
        _ => status.ToString(),
    };

    public static Color ColorOf(DeliveryStopStatus status) => status switch
    {
        DeliveryStopStatus.Delivered => Color.Success,
        DeliveryStopStatus.Failed => Color.Error,
        _ => Color.Default,
    };

    /// <summary>Algorithm names reported by the route optimizer.</summary>
    public static string Algorithm(string algorithm) => algorithm switch
    {
        "BruteForce" => "Przegląd wszystkich kolejności",
        "HeldKarp" => "Held-Karp (programowanie dynamiczne)",
        "NearestNeighbor+2opt+OrOpt" => "Heurystyka: najbliższy sąsiad + 2-opt + Or-opt",
        "Manual" => "Kolejność ustalona ręcznie",
        _ => algorithm,
    };

    public static string MatrixProvider(string provider) => provider switch
    {
        "OSRM" => "OSRM (sieć dróg OpenStreetMap)",
        "Google" => "Google Routes API",
        "Haversine" => "Szacunek w linii prostej",
        _ => provider,
    };

    public static string Format(RouteLocationDto l) => $"{l.Street} {l.BuildingNumber}, {l.PostalCode} {l.City}";

    public static string Format(DeliveryStopDto s) =>
        $"{s.Street} {s.BuildingNumber}{(s.ApartmentNumber is null ? string.Empty : "/" + s.ApartmentNumber)}, {s.PostalCode} {s.City}";

    /// <summary>Polish explanation of an order-draft warning for the admin.</summary>
    public static string Of(DraftWarningDto warning) => warning.Code switch
    {
        DraftWarningCodes.ClientNotMentioned => "Nie rozpoznano klienta – wybierz istniejącego albo uzupełnij dane nowego.",
        DraftWarningCodes.ClientAmbiguous => "Pasuje kilku klientów – wybierz właściwego albo utwórz nowego.",
        DraftWarningCodes.ClientDetailsDiffer =>
            $"Klient rozpoznany po telefonie lub e-mailu, ale podane imię i nazwisko („{warning.Subject}”) jest inne – sprawdź.",
        DraftWarningCodes.AddressNotMentioned => "Nie podano adresu dostawy.",
        DraftWarningCodes.GeocodingFailed => "Nie udało się uzupełnić adresu (kod pocztowy, położenie) – uzupełnij go ręcznie.",
        DraftWarningCodes.ProductNotFound => $"Nie znaleziono produktu „{warning.Subject}”.",
        DraftWarningCodes.ProductAmbiguous => $"Kilka produktów pasuje do „{warning.Subject}” – wybierz właściwy.",
        DraftWarningCodes.StockTooLow => $"Za mały stan magazynowy: {warning.Subject}.",
        DraftWarningCodes.NoProducts => "Nie rozpoznano żadnych produktów.",
        _ => warning.Code,
    };

    /// <summary>"telefon", "e-mail", "imię i nazwisko" – why a client was suggested.</summary>
    public static string Of(ClientMatchReason reason) => reason switch
    {
        ClientMatchReason.Phone => "telefon",
        ClientMatchReason.Email => "e-mail",
        _ => "imię i nazwisko",
    };

    /// <summary>"92 %" – similarity score of a suggestion.</summary>
    public static string Score(double score) => ((int)Math.Round(score * 100, MidpointRounding.AwayFromZero)).ToString(Polish) + " %";
}
