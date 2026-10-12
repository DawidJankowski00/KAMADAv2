using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Matching.Text;

namespace Kamada.Web.Shared.Assistant.Evaluation;

/// <summary>
/// Compares a draft with the expected outcome, by the same rules as the server evals (lines by position: the model keeps
/// the spoken order). Used by the accuracy page of the phone app to measure the on-device models.
/// </summary>
public static class AssistantEvalScorer
{
    public static AssistantEvalResult Failed(AssistantEvalCase evalCase, long latencyMs, string problem) =>
        new(evalCase, latencyMs, false, false, 0, 0, evalCase.Lines.Count, [problem]);

    /// <param name="selectedClient">The client the draft selected, to read the address it selected.</param>
    public static AssistantEvalResult Evaluate(AssistantEvalCase evalCase, OrderDraftDto draft, ClientLookupDto? selectedClient, long latencyMs)
    {
        var problems = new List<string>();
        var clientCorrect = CheckClient(evalCase.Client, draft.Client, problems);
        var addressCorrect = CheckAddress(evalCase.Address, draft.Address, selectedClient, problems);

        var counted = Math.Max(evalCase.Lines.Count, draft.Lines.Count);
        var (products, quantities) = (0, 0);
        for (var i = 0; i < evalCase.Lines.Count; i++)
        {
            var expected = evalCase.Lines[i];
            if (i >= draft.Lines.Count)
            {
                problems.Add($"line {i + 1}: missing (expected {Describe(expected)})");
                continue;
            }

            var actual = draft.Lines[i];
            if (CheckProduct(expected, actual))
            {
                products++;
            }
            else
            {
                problems.Add($"line {i + 1}: expected {Describe(expected)}, got '{actual.SpokenText}' → {Describe(actual)}");
            }

            if (actual.Quantity == expected.Quantity)
            {
                quantities++;
            }
            else
            {
                problems.Add($"line {i + 1}: quantity {actual.Quantity}, expected {expected.Quantity}");
            }
        }

        foreach (var extra in draft.Lines.Skip(evalCase.Lines.Count))
        {
            problems.Add($"extra line '{extra.SpokenText}'");
        }

        return new AssistantEvalResult(evalCase, latencyMs, clientCorrect, addressCorrect, products, quantities, counted, problems);
    }

    private static bool CheckClient(string expected, DraftClientDto actual, List<string> problems)
    {
        var (kind, value) = Split(expected);
        var selected = actual.Candidates.FirstOrDefault(c => c.Id == actual.SelectedId);
        var correct = kind switch
        {
            "Existing" => actual.Mode == DraftClientMode.Existing && selected?.DisplayName.StartsWith(value + " (", StringComparison.Ordinal) == true,
            "New" => actual.Mode == DraftClientMode.New && SameText($"{actual.NewClient?.Name} {actual.NewClient?.Surname}", value),
            _ => actual.Mode == DraftClientMode.Ambiguous,
        };

        if (!correct)
        {
            var got = actual.Mode switch
            {
                DraftClientMode.Existing => "Existing:" + selected?.DisplayName,
                DraftClientMode.New => $"New:{actual.NewClient?.Name} {actual.NewClient?.Surname}",
                _ => "Ambiguous",
            };
            problems.Add($"client: expected {expected}, got {got}");
        }

        return correct;
    }

    private static bool CheckAddress(string expected, DraftAddressDto actual, ClientLookupDto? selectedClient, List<string> problems)
    {
        var (kind, value) = Split(expected);
        string got;
        bool correct;
        switch (actual.Mode)
        {
            case DraftAddressMode.Existing:
                var address = selectedClient?.ShippingAddresses.FirstOrDefault(a => a.Id == actual.SelectedId);
                got = $"Existing:{address?.Street} {address?.BuildingNumber}";
                correct = kind == "Existing" && SameText(got[9..], value);
                break;
            case DraftAddressMode.New:
                var created = actual.NewAddress!;
                got = $"New:{created.Street} {created.BuildingNumber}";
                correct = kind == "New" && SameStreetAndBuilding(created.Street, created.BuildingNumber, value);
                break;
            default:
                got = "None";
                correct = kind == "None";
                break;
        }

        if (!correct)
        {
            problems.Add($"address: expected {expected}, got {got}");
        }

        return correct;
    }

    private static bool CheckProduct(AssistantEvalLine expected, DraftLineDto actual)
    {
        var selected = actual.Candidates.FirstOrDefault(c => c.Id == actual.SelectedProductId);
        if (expected.Sku is not null)
        {
            return selected?.Sku == expected.Sku;
        }

        if (expected.Ambiguous is { } options)
        {
            return selected is null && options.All(sku => actual.Candidates.Any(c => c.Sku == sku));
        }

        return selected is null; // not in the catalogue: nothing may be auto-selected
    }

    /// <summary>"Grunwaldzka 15" vs street "Aleja Grunwaldzka" + building "15".</summary>
    private static bool SameStreetAndBuilding(string? street, string? building, string expected)
    {
        var split = expected.LastIndexOf(' ');
        var (expectedStreet, expectedBuilding) = (expected[..split], expected[(split + 1)..]);
        return TextSimilarity.Fold(street).Contains(TextSimilarity.Fold(expectedStreet), StringComparison.Ordinal)
            && TextSimilarity.Compact(building) == TextSimilarity.Compact(expectedBuilding);
    }

    private static bool SameText(string actual, string expected) => TextSimilarity.Fold(actual) == TextSimilarity.Fold(expected);

    private static (string Kind, string Value) Split(string expected)
    {
        var colon = expected.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? (expected, string.Empty) : (expected[..colon], expected[(colon + 1)..]);
    }

    private static string Describe(AssistantEvalLine line) =>
        line.Sku ?? (line.Ambiguous is { } options ? $"choice of {string.Join("/", options)}" : "no product (not in catalogue)");

    private static string Describe(DraftLineDto line) =>
        line.Candidates.FirstOrDefault(c => c.Id == line.SelectedProductId)?.Sku
        ?? (line.Candidates.Count > 0 ? $"choice of {string.Join("/", line.Candidates.Select(c => c.Sku))}" : "nothing found");
}
