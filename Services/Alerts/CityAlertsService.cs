using ApiAlertaDengue.Contracts;
using ApiAlertaDengue.Data;
using ApiAlertaDengue.Helpers;
using ApiAlertaDengue.Models;
using ApiAlertaDengue.Services.InfoDengue;
using ApiAlertaDengue.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Services.Alerts;

/// <summary>
/// Leitura híbrida: serve do banco; se a cidade nunca foi consultada, busca na fonte externa antes de responder;
/// se os dados estiverem velhos, tenta atualizar e, em caso de falha, devolve o último dado salvo.
/// </summary>
public class CityAlertsService(
    AppDbContext db,
    ICitySyncService sync,
    IOptions<SyncOptions> options,
    TimeProvider timeProvider,
    ILogger<CityAlertsService> logger) : ICityAlertsService
{
    private const int LatestLookbackWeeks = 12;

    public async Task<PagedResult<CityDto>> SearchCitiesAsync(
        string? search, string? state, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.Cities.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(state))
        {
            var uf = state.Trim().ToUpperInvariant();
            query = query.Where(c => c.State == uf);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search.Trim())}%";
            query = query.Where(c => EF.Functions.Like(c.Name, pattern, "\\"));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.Name).ThenBy(c => c.State)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CityDto>(items.Select(CityDto.From).ToList(), page, pageSize, total);
    }

    public async Task<CityDto?> GetCityAsync(int geocode, CancellationToken cancellationToken = default)
    {
        var city = await db.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Geocode == geocode, cancellationToken);
        return city is null ? null : CityDto.From(city);
    }

    public async Task<AlertsOutcome> GetAlertsAsync(
        int geocode, Disease? disease, int weeks, CancellationToken cancellationToken = default)
    {
        var ensured = await EnsureDataAsync(geocode, cancellationToken);
        if (ensured.Status != AlertsStatus.Ok) return new AlertsOutcome(ensured.Status);

        var fromKey = EpidemiologicalWeek.Subtract(CurrentWeek(), weeks - 1).Key;
        var query = db.DengueAlerts.AsNoTracking()
            .Where(a => a.Geocode == geocode && a.Year * 100 + a.Week >= fromKey);
        if (disease is { } d) query = query.Where(a => a.Disease == d);

        var alerts = await query
            .OrderByDescending(a => a.Year).ThenByDescending(a => a.Week).ThenBy(a => a.Disease)
            .ToListAsync(cancellationToken);

        return Ok(ensured, alerts);
    }

    public async Task<AlertsOutcome> GetLatestAsync(int geocode, CancellationToken cancellationToken = default)
    {
        var ensured = await EnsureDataAsync(geocode, cancellationToken);
        if (ensured.Status != AlertsStatus.Ok) return new AlertsOutcome(ensured.Status);

        var fromKey = EpidemiologicalWeek.Subtract(CurrentWeek(), LatestLookbackWeeks - 1).Key;
        var recent = await db.DengueAlerts.AsNoTracking()
            .Where(a => a.Geocode == geocode && a.Year * 100 + a.Week >= fromKey)
            .ToListAsync(cancellationToken);

        var latest = recent
            .GroupBy(a => a.Disease)
            .Select(g => g.OrderByDescending(a => a.Year).ThenByDescending(a => a.Week).First())
            .OrderBy(a => a.Disease)
            .ToList();

        return Ok(ensured, latest);
    }

    private static string EscapeLike(string term) =>
        term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private record Ensured(AlertsStatus Status, CityDto? City = null, bool IsStale = false);

    private async Task<Ensured> EnsureDataAsync(int geocode, CancellationToken cancellationToken)
    {
        var city = await db.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Geocode == geocode, cancellationToken);
        if (city is null) return new Ensured(AlertsStatus.CityNotFound);

        var staleAfter = TimeSpan.FromDays(options.Value.StaleAfterDays);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var needsSync = city.LastSyncedAt is not { } last || now - last >= staleAfter;

        if (needsSync)
        {
            try
            {
                await sync.SyncCityAsync(geocode, SyncMode.IfNeeded, cancellationToken);
            }
            catch (InfoDengueException ex)
            {
                if (city.LastSyncedAt is null)
                {
                    logger.LogWarning(ex, "Cidade {Geocode} sem dados e fonte externa indisponível.", geocode);
                    return new Ensured(AlertsStatus.SourceUnavailable);
                }

                logger.LogWarning(ex, "Falha ao atualizar a cidade {Geocode}; servindo dados salvos.", geocode);
            }

            city = await db.Cities.AsNoTracking().FirstAsync(c => c.Geocode == geocode, cancellationToken);
        }

        var isStale = city.LastSyncedAt is not { } synced || now - synced >= staleAfter;
        return new Ensured(AlertsStatus.Ok, CityDto.From(city), isStale);
    }

    private static AlertsOutcome Ok(Ensured ensured, IEnumerable<DengueAlert> alerts) =>
        new(AlertsStatus.Ok, new CityAlertsResponse(
            ensured.City!, ensured.City!.LastSyncedAt, ensured.IsStale, alerts.Select(AlertDto.From).ToList()));

    private EpidemiologicalWeek.Week CurrentWeek() =>
        EpidemiologicalWeek.From(DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
}
