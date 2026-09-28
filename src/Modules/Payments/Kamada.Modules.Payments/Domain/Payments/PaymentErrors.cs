using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Payments.Domain.Payments;

internal static class PaymentErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Payments.NotFound", "Payment was not found.");

    public static readonly Error OrderNotAwaitingPayment =
        Error.Conflict("Payments.OrderNotAwaitingPayment", "The order is not waiting for payment.");

    public static readonly Error AlreadyInProgress =
        Error.Conflict("Payments.AlreadyInProgress", "The order already has a pending or completed payment.");

    public static readonly Error RefundRequiresCancelledOrder =
        Error.Conflict("Payments.RefundRequiresCancelledOrder", "Only payments of cancelled orders can be refunded.");

    public static readonly Error NotATransfer =
        Error.Conflict("Payments.NotATransfer", "Only bank transfers are confirmed manually.");

    public static Error InvalidTransition(PaymentStatus from, PaymentStatus to) =>
        Error.Conflict("Payments.InvalidTransition", $"Payment cannot change status from {from} to {to}.");
}
