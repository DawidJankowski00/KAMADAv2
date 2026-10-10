using System.Globalization;
using System.Text;

namespace Kamada.Assistant.Evals;

/// <summary>Markdown summary: accuracy per field, latency percentiles and the failed cases.</summary>
internal static class EvalReport
{
    public static string Build(string model, IReadOnlyList<CaseResult> results)
    {
        var lines = results.Sum(r => r.LinesCounted);
        var latencies = results.Select(r => r.LatencyMs).Order().ToList();
        var builder = new StringBuilder();

        builder.AppendLine(CultureInfo.InvariantCulture, $"## Assistant evals – `{model}` ({results.Count} cases, {DateTime.Now:yyyy-MM-dd})");
        builder.AppendLine();
        builder.AppendLine("| Model | Client decision | Address decision | Product | Quantity | All correct | p50 | p95 |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|");
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"| `{model}` | {Percent(results.Count(r => r.ClientCorrect), results.Count)} | {Percent(results.Count(r => r.AddressCorrect), results.Count)} " +
            $"| {Percent(results.Sum(r => r.ProductsCorrect), lines)} | {Percent(results.Sum(r => r.QuantitiesCorrect), lines)} " +
            $"| {Percent(results.Count(r => r.AllCorrect), results.Count)} | {Seconds(Percentile(latencies, 0.50))} | {Seconds(Percentile(latencies, 0.95))} |");

        foreach (var language in results.GroupBy(r => r.Case.Language))
        {
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"| ↳ {language.Key} ({language.Count()}) | {Percent(language.Count(r => r.ClientCorrect), language.Count())} " +
                $"| {Percent(language.Count(r => r.AddressCorrect), language.Count())} " +
                $"| {Percent(language.Sum(r => r.ProductsCorrect), language.Sum(r => r.LinesCounted))} " +
                $"| {Percent(language.Sum(r => r.QuantitiesCorrect), language.Sum(r => r.LinesCounted))} " +
                $"| {Percent(language.Count(r => r.AllCorrect), language.Count())} | | |");
        }

        var failed = results.Where(r => !r.AllCorrect).ToList();
        if (failed.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("### Mistakes");
            builder.AppendLine();
            foreach (var result in failed)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"- **{result.Case.Id}**: {string.Join("; ", result.Problems)}");
            }
        }

        return builder.ToString();
    }

    private static string Percent(int correct, int total) =>
        total == 0 ? "–" : string.Create(CultureInfo.InvariantCulture, $"{100.0 * correct / total:0}% ({correct}/{total})");

    private static string Seconds(long milliseconds) => string.Create(CultureInfo.InvariantCulture, $"{milliseconds / 1000.0:0.0} s");

    private static long Percentile(List<long> sorted, double percentile) =>
        sorted.Count == 0 ? 0 : sorted[(int)Math.Ceiling(percentile * sorted.Count) - 1];
}
