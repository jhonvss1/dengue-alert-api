using ApiAlertaDengue.Models;

namespace ApiAlertaDengue.Contracts;

public record AlertDto(
    string Disease,
    int Year,
    int Week,
    DateOnly? WeekStart,
    double EstimatedCases,
    int ReportedCases,
    double? IncidencePer100k,
    int AlertLevel,
    DateTime UpdatedAt)
{
    public static AlertDto From(DengueAlert a) => new(
        a.Disease.ToApiName(), a.Year, a.Week, a.WeekStart, a.EstimatedCases, a.ReportedCases,
        a.IncidencePer100k, (int)a.AlertLevel, a.UpdatedAt);
}

/// <param name="IsStale">
/// Verdadeiro quando a atualização da fonte externa falhou e os dados devolvidos são os últimos salvos.
/// </param>
public record CityAlertsResponse(CityDto City, DateTime? LastSyncedAt, bool IsStale, IReadOnlyList<AlertDto> Alerts);
