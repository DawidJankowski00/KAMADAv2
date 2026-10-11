using FluentValidation;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Customers;

namespace Kamada.Modules.Customers.Application.Clients;

internal static class CustomerRules
{
    public const int UsernameMaxLength = 50;
    public const int EmailMaxLength = 254;
    public const int NameMaxLength = 100;
    public const int PhoneNumberMaxLength = 20;
    public const int StreetMaxLength = 200;
    public const int BuildingNumberMaxLength = 20;
    public const int CityMaxLength = 100;
    public const int PostalCodeMaxLength = 10;
    public const int CountryMaxLength = 60;
    public const int MaxAddressesOnCreate = 10;

    private static readonly HashSet<string> PolandNames = new(["Polska", "Poland", "PL"], StringComparer.OrdinalIgnoreCase);

    public static bool IsPoland(string? country) => country is not null && PolandNames.Contains(country.Trim());
}

internal sealed class AddressRequestValidator : AbstractValidator<AddressRequest>
{
    public AddressRequestValidator()
    {
        RuleFor(a => a.Type).IsInEnum();
        RuleFor(a => a.Street).NotEmpty().MaximumLength(CustomerRules.StreetMaxLength);
        RuleFor(a => a.BuildingNumber).NotEmpty().MaximumLength(CustomerRules.BuildingNumberMaxLength);
        RuleFor(a => a.ApartmentNumber).MaximumLength(CustomerRules.BuildingNumberMaxLength);
        RuleFor(a => a.City).NotEmpty().MaximumLength(CustomerRules.CityMaxLength);
        RuleFor(a => a.Country).NotEmpty().MaximumLength(CustomerRules.CountryMaxLength);
        RuleFor(a => a.PostalCode).NotEmpty().MaximumLength(CustomerRules.PostalCodeMaxLength);
        RuleFor(a => a.PostalCode)
            .Matches("^[0-9]{2}-[0-9]{3}$")
            .When(a => CustomerRules.IsPoland(a.Country))
            .WithMessage("Polish postal code must have the format 00-000.");
        RuleFor(a => a.RequestId).NotEqual(Guid.Empty);
    }
}

internal sealed class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    public CreateClientRequestValidator()
    {
        RuleFor(r => r.Username)
            .NotEmpty()
            .Length(3, CustomerRules.UsernameMaxLength)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Username may contain only letters, digits, '.', '_' and '-'.");
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(CustomerRules.EmailMaxLength);
        RuleFor(r => r.Name).NotEmpty().MaximumLength(CustomerRules.NameMaxLength);
        RuleFor(r => r.Surname).NotEmpty().MaximumLength(CustomerRules.NameMaxLength);
        RuleFor(r => r.PhoneNumber).ValidPhoneNumber();
        RuleFor(r => r.Addresses)
            .Must(a => a is null || a.Count <= CustomerRules.MaxAddressesOnCreate)
            .WithMessage($"At most {CustomerRules.MaxAddressesOnCreate} addresses can be added at once.");
        RuleForEach(r => r.Addresses).SetValidator(new AddressRequestValidator());
        RuleFor(r => r.RequestId).NotEqual(Guid.Empty);
    }
}

internal sealed class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    public UpdateClientRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(CustomerRules.EmailMaxLength);
        RuleFor(r => r.Name).NotEmpty().MaximumLength(CustomerRules.NameMaxLength);
        RuleFor(r => r.Surname).NotEmpty().MaximumLength(CustomerRules.NameMaxLength);
        RuleFor(r => r.PhoneNumber).ValidPhoneNumber();
    }
}

internal sealed class SetAddressLocationRequestValidator : AbstractValidator<SetAddressLocationRequest>
{
    public SetAddressLocationRequestValidator()
    {
        RuleFor(r => r.Latitude).InclusiveBetween(-90, 90);
        RuleFor(r => r.Longitude).InclusiveBetween(-180, 180);
    }
}

internal sealed class ClientListQueryValidator : AbstractValidator<ClientListQuery>
{
    public ClientListQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

internal static class PhoneRuleExtensions
{
    public static IRuleBuilderOptions<T, string> ValidPhoneNumber<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MaximumLength(CustomerRules.PhoneNumberMaxLength)
            .Matches("^[+]?[0-9 ]{9,19}$")
            .WithMessage("Phone number must contain 9-19 digits, optionally prefixed with '+'.");
}
