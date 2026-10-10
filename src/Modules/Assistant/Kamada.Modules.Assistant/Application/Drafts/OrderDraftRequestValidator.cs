using FluentValidation;
using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Contracts.Assistant;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Application.Drafts;

internal sealed class OrderDraftRequestValidator : AbstractValidator<OrderDraftRequest>
{
    public OrderDraftRequestValidator(IOptions<AssistantOptions> options)
    {
        RuleFor(r => r.Text).NotEmpty().MaximumLength(options.Value.MaxTextLength);
        RuleFor(r => r.Language)
            .Must(AssistantLanguages.IsValid)
            .WithMessage($"Language must be one of: {string.Join(", ", AssistantLanguages.Codes)}.");
    }
}
