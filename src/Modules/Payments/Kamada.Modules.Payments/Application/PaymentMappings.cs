using FluentValidation;
using Kamada.Modules.Payments.Domain.Payments;
using Kamada.Shared.Contracts.Payments;
using ContractProvider = Kamada.Shared.Contracts.Payments.PaymentProvider;
using ContractStatus = Kamada.Shared.Contracts.Payments.PaymentStatus;

namespace Kamada.Modules.Payments.Application;

internal static class PaymentMappings
{
    public static PaymentDto ToDto(this Payment payment) => new(
        payment.Id,
        payment.OrderId,
        Enum.Parse<ContractProvider>(payment.Provider.ToString()),
        payment.TransactionId,
        payment.Amount,
        Enum.Parse<ContractStatus>(payment.Status.ToString()),
        payment.FailureReason,
        payment.PaidAt,
        payment.CreatedAt);
}

internal sealed class RegisterPaymentRequestValidator : AbstractValidator<RegisterPaymentRequest>
{
    public RegisterPaymentRequestValidator() => RuleFor(r => r.Provider).IsInEnum();
}
