using ApiAlertaDengue.Data;
using ApiAlertaDengue.Helpers;
using ApiAlertaDengue.Models;
using ApiAlertaDengue.Services.InfoDengue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Services.Sync;

public class CitySyncService(
    AppDbContext db,
    IInfoDengueClient client,
    CitySyncLocks locks,
    IOptions<SyncOptions> options,
    TimeProvider timeProvider,
    ILogger<CitySyncService> logger) : ICitySyncService
{
    private static readonly Disease[] Diseases = Enum.GetValues<Disease>();

    public async Task<CitySyncResult> SyncCityAsync(int geocode, SyncMode mode, CancellationToken cancellationToken = default)
    {
        using var _ = await locks.AcquireAsync(geocode, cancellationToken);

        var city = await db.Cities.FirstOrDefaultAsync(c => c.Geocode == geocode, cancellationToken);
        if (city is null) return new CitySyncResult(CitySyncStatus.CityNotFound);

        // Outra requisição pode ter sincronizado a cidade enquanto esperávamos a trava.
        await db.Entry(city).ReloadAsync(cancellationToken);

        var settings = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (mode == SyncMode.IfNeeded && city.LastSyncedAt is { } last
            && now - last < TimeSpan.FromDays(settings.StaleAfterDays))
        {
            return new CitySyncResult(CitySyncStatus.Skipped);
        }

        var to = EpidemiologicalWeek.From(DateOnly.FromDateTime(now));
        var from = EpidemiologicalWeek.Subtract(to, WeeksToFetch(city.LastSyncedAt, now, settings));

        var fetched = new List<DengueAlert>();
        foreach (var disease in Diseases)
        {
            fetched.AddRange(await client.GetAlertsAsync(geocode, disease, from, to, cancellationToken));
        }

        var written = await UpsertAsync(geocode, fetched, from, now, cancellationToken);

        city.IsTracked = true;
        city.LastSyncedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Cidade {Geocode} sincronizada: {Fetched} registros recebidos, {Written} gravados.",
            geocode, fetched.Count, written);
        return new CitySyncResult(CitySyncStatus.Synced, written);
    }

    /// <summary>Primeira vez: histórico completo. Depois: desde a última sincronização, com a janela de revisão.</summary>
    internal static int WeeksToFetch(DateTime? lastSyncedAt, DateTime now, SyncOptions settings)
    {
        if (lastSyncedAt is not { } last) return settings.InitialHistoryWeeks;

        var weeksSince = (int)Math.Ceiling((now - last).TotalDays / 7);
        return Math.Clamp(weeksSince + settings.RefreshWindowWeeks, settings.RefreshWindowWeeks, settings.InitialHistoryWeeks);
    }

    private async Task<int> UpsertAsync(
        int geocode, List<DengueAlert> fetched, EpidemiologicalWeek.Week from, DateTime now, CancellationToken cancellationToken)
    {
        if (fetched.Count == 0) return 0;

        // Considera também semanas mais antigas que a janela pedida, caso a fonte as devolva.
        var fromKey = Math.Min(from.Key, fetched.Min(a => a.Year * 100 + a.Week));
        var existing = (await db.DengueAlerts
                .Where(a => a.Geocode == geocode && a.Year * 100 + a.Week >= fromKey)
                .ToListAsync(cancellationToken))
            .ToDictionary(a => (a.Disease, a.Year, a.Week));

        var written = 0;
        foreach (var incoming in fetched)
        {
            if (!existing.TryGetValue((incoming.Disease, incoming.Year, incoming.Week), out var current))
            {
                incoming.CreatedAt = incoming.UpdatedAt = now;
                db.DengueAlerts.Add(incoming);
                written++;
            }
            else if (HasChanged(current, incoming))
            {
                current.WeekStart = incoming.WeekStart;
                current.EstimatedCases = incoming.EstimatedCases;
                current.ReportedCases = incoming.ReportedCases;
                current.IncidencePer100k = incoming.IncidencePer100k;
                current.AlertLevel = incoming.AlertLevel;
                current.UpdatedAt = now;
                written++;
            }
        }

        return written;
    }

    private static bool HasChanged(DengueAlert current, DengueAlert incoming) =>
        current.WeekStart != incoming.WeekStart
        || current.EstimatedCases != incoming.EstimatedCases
        || current.ReportedCases != incoming.ReportedCases
        || current.IncidencePer100k != incoming.IncidencePer100k
        || current.AlertLevel != incoming.AlertLevel;
}
