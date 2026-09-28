using FluentValidation;
using Kamada.Modules.Delivery.Domain.Runs;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Delivery;

namespace Kamada.Modules.Delivery.Application;

internal static class DeliveryRules
{
    public const int NameMaxLength = 100;
    public const int VehiclePlateMaxLength = 20;
    public const int FailureReasonMaxLength = 500;

    private static readonly HashSet<string> PolandNames = new(["Polska", "Poland", "PL"], StringComparer.OrdinalIgnoreCase);

    public static bool IsPoland(string? country) => country is not null && PolandNames.Contains(country.Trim());
}

internal sealed class RouteLocationRequestValidator : AbstractValidator<RouteLocationRequest>
{
    public RouteLocationRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(DeliveryRules.NameMaxLength);
        RuleFor(r => r.Street).NotEmpty().MaximumLength(200);
        RuleFor(r => r.BuildingNumber).NotEmpty().MaximumLength(20);
        RuleFor(r => r.City).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Country).NotEmpty().MaximumLength(60);
        RuleFor(r => r.PostalCode).NotEmpty().MaximumLength(10);
        RuleFor(r => r.PostalCode)
            .Matches("^[0-9]{2}-[0-9]{3}$")
            .When(r => DeliveryRules.IsPoland(r.Country))
            .WithMessage("Polish postal code must have the format 00-000.");
    }
}

internal sealed class SetRouteLocationPinRequestValidator : AbstractValidator<SetRouteLocationPinRequest>
{
    public SetRouteLocationPinRequestValidator()
    {
        RuleFor(r => r.Latitude).InclusiveBetween(-90, 90);
        RuleFor(r => r.Longitude).InclusiveBetween(-180, 180);
    }
}

internal sealed class CreateDeliveryRunRequestValidator : AbstractValidator<CreateDeliveryRunRequest>
{
    public CreateDeliveryRunRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(DeliveryRules.NameMaxLength);
        RuleFor(r => r.CourierId).NotEmpty();
        RuleFor(r => r.VehiclePlate).ValidVehiclePlate();
        RuleFor(r => r.StartLocationId).NotEmpty();
        RuleFor(r => r.EndLocationId).NotEmpty();
        RuleFor(r => r.OrderIds!).ValidOrderIds().When(r => r.OrderIds is { Count: > 0 });
    }
}

internal sealed class UpdateDeliveryRunRequestValidator : AbstractValidator<UpdateDeliveryRunRequest>
{
    public UpdateDeliveryRunRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(DeliveryRules.NameMaxLength);
        RuleFor(r => r.CourierId).NotEmpty();
        RuleFor(r => r.VehiclePlate).ValidVehiclePlate();
        RuleFor(r => r.StartLocationId).NotEmpty();
        RuleFor(r => r.EndLocationId).NotEmpty();
    }
}

internal sealed class AddRunOrdersRequestValidator : AbstractValidator<AddRunOrdersRequest>
{
    public AddRunOrdersRequestValidator() => RuleFor(r => r.OrderIds).NotEmpty().ValidOrderIds();
}

internal sealed class SetRunSequenceRequestValidator : AbstractValidator<SetRunSequenceRequest>
{
    public SetRunSequenceRequestValidator() => RuleFor(r => r.OrderIds).NotEmpty().ValidOrderIds();
}

internal sealed class FailStopRequestValidator : AbstractValidator<FailStopRequest>
{
    public FailStopRequestValidator() => RuleFor(r => r.Reason).NotEmpty().MaximumLength(DeliveryRules.FailureReasonMaxLength);
}

internal sealed class DeliveryRunListQueryValidator : AbstractValidator<DeliveryRunListQuery>
{
    public DeliveryRunListQueryValidator()
    {
        RuleFor(q => q.Status).IsInEnum();
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.To).GreaterThanOrEqualTo(q => q.From).When(q => q.From is not null && q.To is not null);
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

internal sealed class CourierRunListQueryValidator : AbstractValidator<CourierRunListQuery>
{
    public CourierRunListQueryValidator() => RuleFor(q => q.Scope).IsInEnum();
}

internal static class DeliveryRuleExtensions
{
    public static IRuleBuilderOptions<T, string> ValidVehiclePlate<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MaximumLength(DeliveryRules.VehiclePlateMaxLength)
            .Matches("^[A-Za-z0-9 ]+$")
            .WithMessage("Vehicle plate may contain only letters, digits and spaces.");

    public static IRuleBuilderOptions<T, IReadOnlyList<Guid>> ValidOrderIds<T>(this IRuleBuilder<T, IReadOnlyList<Guid>> rule) =>
        rule.Must(ids => ids.Count <= DeliveryRun.MaxStops)
            .WithMessage($"A delivery run can have at most {DeliveryRun.MaxStops} stops.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Each order can be listed only once.")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("Order ids must not be empty.");
}
