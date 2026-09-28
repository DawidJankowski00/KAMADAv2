using FluentValidation;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Ordering;

namespace Kamada.Modules.Ordering.Application.Orders;

internal sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public const int MaxLines = 100;
    public const int MaxQuantityPerLine = 10_000;

    public PlaceOrderRequestValidator()
    {
        RuleFor(r => r.ClientId).NotEmpty();
        RuleFor(r => r.ShippingAddressId).NotEmpty();
        RuleFor(r => r.Items).NotEmpty().Must(i => i.Count <= MaxLines).WithMessage($"An order can have at most {MaxLines} lines.");
        RuleForEach(r => r.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, MaxQuantityPerLine);
        });
        RuleFor(r => r.Notes).MaximumLength(1000);
    }
}

internal sealed class OrderListQueryValidator : AbstractValidator<OrderListQuery>
{
    public OrderListQueryValidator()
    {
        RuleFor(q => q.Status).IsInEnum();
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.To).GreaterThan(q => q.From).When(q => q.From is not null && q.To is not null);
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}
