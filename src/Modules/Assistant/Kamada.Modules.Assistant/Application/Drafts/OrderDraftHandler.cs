using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Assistant.Application.Drafts;

/// <summary>Text → mentions (language model) → draft matched against real data (C#). Never writes anything.</summary>
internal sealed class OrderDraftHandler(IOrderExtractor extractor, OrderDraftResolver resolver)
{
    public async Task<Result<OrderDraftDto>> CreateAsync(OrderDraftRequest request, CancellationToken cancellationToken)
    {
        var text = request.Text.Trim();
        var extraction = await extractor.ExtractAsync(text, AssistantLanguages.Parse(request.Language), cancellationToken);
        if (extraction.IsFailure)
        {
            return extraction.Error;
        }

        if (extraction.Value.IsEmpty)
        {
            return AssistantErrors.NothingRecognized;
        }

        return await resolver.ResolveAsync(extraction.Value, text, cancellationToken);
    }
}
