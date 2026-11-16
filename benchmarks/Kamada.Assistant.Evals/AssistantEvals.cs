using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Contracts.Customers;

namespace Kamada.Assistant.Evals;

/// <summary>
/// Measures the order assistant end to end (real model + real matching against the seeded demo database):
///   docker compose up -d postgres; docker compose --profile ai up -d ollama
///   dotnet run -c Release --project benchmarks/Kamada.Assistant.Evals -- --model gemma3:4b [--out results.md]
///   GEMINI_API_KEY=... dotnet run -c Release --project benchmarks/Kamada.Assistant.Evals -- --provider gemini [--model gemini-3.5-flash-lite]
/// An Ollama model must be pulled first (ollama pull MODEL). Notes are sent as typed text – speech recognition is not part of it.
/// </summary>
/// <remarks>No top-level statements: the API's <c>Program</c> is the host type of <see cref="EvalHost"/>.</remarks>
internal static class AssistantEvals
{
    public static async Task Main(string[] args)
    {
        var options = EvalOptions.Parse(args);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var cases = JsonSerializer.Deserialize<List<EvalCase>>(await File.ReadAllTextAsync(options.CasesPath), json)!;

        var via = options.IsGemini ? "the Gemini API" : options.OllamaUrl.ToString();
        Console.WriteLine($"Model {options.Model} via {via} – {cases.Count} cases; starting the API (first run seeds the demo database)…");
        await using var host = new EvalHost(options);
        using var http = host.CreateClient();
        await host.SelectProviderAsync();
        http.Timeout = TimeSpan.FromMinutes(6);

        // Demo administrator from the seed data (WorkforceSeeder). Signed in again before every case: a run with a slow
        // model outlasts an access token.
        async Task SignInAsync()
        {
            var login = await http.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Admin123!"), json);
            login.EnsureSuccessStatusCode();
            var tokens = (await login.Content.ReadFromJsonAsync<LoginResponse>(json))!.Tokens!;
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        }

        await SignInAsync();

        // The first request loads the model into memory – not counted.
        Console.WriteLine("Warming up the model…");
        await http.PostAsJsonAsync("/api/assistant/order-drafts", new OrderDraftRequest("Zamówienie dla Jana: jedna kawa.", "pl"), json);

        var results = new List<CaseResult>();
        foreach (var evalCase in cases)
        {
            await SignInAsync();
            var stopwatch = Stopwatch.StartNew();
            var response = await http.PostAsJsonAsync("/api/assistant/order-drafts", new OrderDraftRequest(evalCase.Text, evalCase.Language), json);
            stopwatch.Stop();

            CaseResult result;
            if (!response.IsSuccessStatusCode)
            {
                result = DraftEvaluator.Failed(evalCase, stopwatch.ElapsedMilliseconds, $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            }
            else
            {
                var draft = (await response.Content.ReadFromJsonAsync<OrderDraftDto>(json))!;
                var client = draft.Client.SelectedId is { } id ? await http.GetFromJsonAsync<ClientDto>($"/api/clients/{id}", json) : null;
                result = DraftEvaluator.Evaluate(evalCase, draft, client, stopwatch.ElapsedMilliseconds);
            }

            results.Add(result);
            Console.WriteLine($"{(result.AllCorrect ? "ok  " : "FAIL")} {evalCase.Id,-40} {stopwatch.ElapsedMilliseconds,6} ms");
        }

        var report = EvalReport.Build(options.Label, results);
        Console.WriteLine();
        Console.WriteLine(report);
        if (options.OutputPath is not null)
        {
            await File.WriteAllTextAsync(options.OutputPath, report);
            Console.WriteLine($"Report written to {options.OutputPath}");
        }
    }
}
