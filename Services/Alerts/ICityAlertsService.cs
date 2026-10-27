using ApiAlertaDengue.Contracts;
using ApiAlertaDengue.Models;

namespace ApiAlertaDengue.Services.Alerts;

public enum AlertsStatus
{
    Ok,
    CityNotFound,

    /// <summary>A cidade ainda não tem dados e a fonte externa está indisponível.</summary>
    SourceUnavailable,
}

public record AlertsOutcome(AlertsStatus Status, CityAlertsResponse? Response = null);

public interface ICityAlertsService
{
    Task<PagedResult<CityDto>> SearchCitiesAsync(
        string? search, string? state, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<CityDto?> GetCityAsync(int geocode, CancellationToken cancellationToken = default);

    /// <summary>Série semanal das últimas <paramref name="weeks"/> semanas (todas as doenças se <paramref name="disease"/> for nulo).</summary>
    Task<AlertsOutcome> GetAlertsAsync(
        int geocode, Disease? disease, int weeks, CancellationToken cancellationToken = default);

    /// <summary>Semana mais recente disponível de cada doença.</summary>
    Task<AlertsOutcome> GetLatestAsync(int geocode, CancellationToken cancellationToken = default);
}
