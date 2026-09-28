using System.Globalization;
using Kamada.Shared.Contracts.Customers;
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

    public static string Format(AddressDto a) =>
        $"{a.Street} {a.BuildingNumber}{(a.ApartmentNumber is null ? string.Empty : "/" + a.ApartmentNumber)}, {a.PostalCode} {a.City}";

    public static string Format(ShippingAddressDto a) =>
        $"{a.Street} {a.BuildingNumber}{(a.ApartmentNumber is null ? string.Empty : "/" + a.ApartmentNumber)}, {a.PostalCode} {a.City}";
}
