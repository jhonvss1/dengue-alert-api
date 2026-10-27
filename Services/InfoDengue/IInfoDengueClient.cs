using ApiAlertaDengue.Helpers;
using ApiAlertaDengue.Models;

namespace ApiAlertaDengue.Services.InfoDengue;

public interface IInfoDengueClient
{
    /// <summary>
    /// Busca os alertas semanais de uma doença para um município, no intervalo de semanas informado (inclusive).
    /// Lança <see cref="InfoDengueException"/> se a fonte externa falhar.
    /// </summary>
    Task<IReadOnlyList<DengueAlert>> GetAlertsAsync(
        int geocode,
        Disease disease,
        EpidemiologicalWeek.Week from,
        EpidemiologicalWeek.Week to,
        CancellationToken cancellationToken = default);
}

public class InfoDengueException(string message, Exception? inner = null) : Exception(message, inner);
