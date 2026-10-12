using System.Globalization;
using System.Text;

namespace Kamada.Web.Shared.Assistant.Evaluation;

/// <summary>Markdown summary in the format of the server evals (README table): accuracy per field and latency percentiles.</summary>
public static class AssistantEvalReport
{
    /// <param name="subject">What was measured, e.g. <c>Qwen 2.5 0,5B + Whisper tiny on Pixel 8</c>.</param>
    public static string Build(string subject, IReadOnlyList<AssistantEvalResult> results, DateOnly date)
    {
        var lines = results.Sum(r => r.LinesCounted);
        var latencies = results.Select(r => r.LatencyMs).Order().ToList();
        var builder = new StringBuilder();

        builder.AppendLine(CultureInfo.InvariantCulture, $"## On-device assistant evals – {subject} ({results.Count} cases, {date:yyyy-MM-dd})");
        builder.AppendLine();
        builder.AppendLine("| Client decision | Address decision | Product | Quantity | All correct | p50 | p95 |");
        builder.AppendLine("|---|---|---|---|---|---|---|");
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"| {Percent(results.Count(r => r.ClientCorrect), results.Count)} | {Percent(results.Count(r => r.AddressCorrect), results.Count)} " +
            $"| {Percent(results.Sum(r => r.ProductsCorrect), lines)} | {Percent(results.Sum(r => r.QuantitiesCorrect), lines)} " +
            $"| {Percent(results.Count(r => r.AllCorrect), results.Count)} | {Seconds(Percentile(latencies, 0.50))} | {Seconds(Percentile(latencies, 0.95))} |");

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

    public static string Percent(int correct, int total) =>
        total == 0 ? "–" : string.Create(CultureInfo.InvariantCulture, $"{100.0 * correct / total:0}% ({correct}/{total})");

    public static string Seconds(long milliseconds) => string.Create(CultureInfo.InvariantCulture, $"{milliseconds / 1000.0:0.0} s");

    public static long Percentile(List<long> sorted, double percentile) =>
        sorted.Count == 0 ? 0 : sorted[(int)Math.Ceiling(percentile * sorted.Count) - 1];
}
