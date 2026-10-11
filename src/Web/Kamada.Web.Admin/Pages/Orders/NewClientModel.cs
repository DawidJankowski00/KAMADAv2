using System.Text.RegularExpressions;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Contracts.Customers;

namespace Kamada.Web.Admin.Pages.Orders;

/// <summary>
/// Client to be created together with the order. Rules mirror the API validators, so the submit button is enabled
/// only for data the API accepts. The assistant never invents an e-mail or phone: those stay empty until typed in.
/// </summary>
public sealed partial class NewClientModel
{
    public string Name { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string? NameError => Required(Name);

    public string? SurnameError => Required(Surname);

    public string? PhoneError =>
        Required(Phone) ?? (PhonePattern().IsMatch(Phone.Trim()) ? null : "Telefon: 9–19 cyfr, opcjonalnie z „+” na początku");

    public string? EmailError =>
        Required(Email) ?? (EmailPattern().IsMatch(Email.Trim()) ? null : "Niepoprawny adres e-mail");

    public string? UsernameError =>
        Required(Username) ?? (UsernamePattern().IsMatch(Username.Trim()) ? null : "3–50 znaków: litery, cyfry, „.”, „_”, „-”");

    public bool IsValid => NameError is null && SurnameError is null && PhoneError is null && EmailError is null && UsernameError is null;

    public static NewClientModel From(NewClientDto client) => new()
    {
        Name = client.Name ?? string.Empty,
        Surname = client.Surname ?? string.Empty,
        Phone = client.Phone ?? string.Empty,
        Email = client.Email ?? string.Empty,
        Username = client.Username,
    };

    public static NewClientModel From(CreateClientRequest client) => new()
    {
        Name = client.Name,
        Surname = client.Surname,
        Phone = client.PhoneNumber,
        Email = client.Email,
        Username = client.Username,
    };

    public CreateClientRequest ToRequest(AddressRequest shippingAddress) =>
        new(Username.Trim(), Email.Trim(), Name.Trim(), Surname.Trim(), Phone.Trim(), [shippingAddress]);

    private static string? Required(string value) => string.IsNullOrWhiteSpace(value) ? "Wymagane" : null;

    [GeneratedRegex("^[+]?[0-9 ]{9,19}$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex("^[a-zA-Z0-9._-]{3,50}$")]
    private static partial Regex UsernamePattern();
}
