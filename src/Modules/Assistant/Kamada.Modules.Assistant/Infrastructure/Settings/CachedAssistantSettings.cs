using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Infrastructure.Settings;

/// <summary>
/// Singleton cache of the settings row. Reads in its own scope (its own connection), so it never joins a request's
/// transaction. Two instances creating the missing row at once: the loser reads the winner's row.
/// </summary>
internal sealed class CachedAssistantSettings(
    IServiceScopeFactory scopeFactory,
    IOptions<AssistantOptions> options,
    TimeProvider timeProvider) : IAssistantSettings, IDisposable
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Entry? _entry;

    public async Task<AssistantSettingsSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        if (Fresh(Volatile.Read(ref _entry)) is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (Fresh(Volatile.Read(ref _entry)) is { } loaded)
            {
                return loaded;
            }

            var snapshot = await LoadAsync(cancellationToken);
            Volatile.Write(ref _entry, new Entry(snapshot, timeProvider.GetTimestamp()));
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => Volatile.Write(ref _entry, null);

    public void Dispose() => _gate.Dispose();

    private AssistantSettingsSnapshot? Fresh(Entry? entry) =>
        entry is not null && timeProvider.GetElapsedTime(entry.LoadedAt) < CacheDuration ? entry.Value : null;

    private async Task<AssistantSettingsSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAssistantDbContext>();
        var settings = await db.Settings.AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                       ?? await CreateAsync(db, cancellationToken);
        return ToSnapshot(settings);
    }

    private async Task<AssistantSettings> CreateAsync(IAssistantDbContext db, CancellationToken cancellationToken)
    {
        var config = options.Value;
        var created = AssistantSettings.Create(
            enabled: true, config.Provider, config.Ollama.Model, config.Gemini.Model, config.SpeechProvider);
        if (created.IsFailure)
        {
            throw new InvalidOperationException($"Assistant settings cannot start from the configuration: {created.Error.Description}");
        }

        db.Settings.Add(created.Value);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created.Value;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            db.Settings.Entry(created.Value).State = EntityState.Detached;
            return await db.Settings.AsNoTracking().SingleAsync(cancellationToken);
        }
    }

    private static AssistantSettingsSnapshot ToSnapshot(AssistantSettings s) =>
        new(s.Enabled, s.TextProvider, s.OllamaModel, s.GeminiModel, s.SpeechProvider, s.UpdatedAt);

    private sealed record Entry(AssistantSettingsSnapshot Value, long LoadedAt);
}
