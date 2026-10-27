using ApiAlertaDengue.Contracts;
using ApiAlertaDengue.Models;
using ApiAlertaDengue.Services.Alerts;
using Microsoft.AspNetCore.Mvc;

namespace ApiAlertaDengue.Controllers;

[ApiController]
[Route("api/cities")]
public class CitiesController(ICityAlertsService alerts) : ControllerBase
{
    private const int MaxPageSize = 100;
    private const int MaxWeeks = 104;

    /// <summary>Busca municípios por nome e/ou UF (paginado).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CityDto>>> Search(
        [FromQuery] string? search,
        [FromQuery] string? state,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) return BadRequest(Problem("page deve ser maior ou igual a 1."));
        if (pageSize is < 1 or > MaxPageSize) return BadRequest(Problem($"pageSize deve estar entre 1 e {MaxPageSize}."));

        return await alerts.SearchCitiesAsync(search, state, page, pageSize, cancellationToken);
    }

    [HttpGet("{geocode:int}")]
    public async Task<ActionResult<CityDto>> Get(int geocode, CancellationToken cancellationToken)
    {
        var city = await alerts.GetCityAsync(geocode, cancellationToken);
        return city is null ? NotFound(Problem("Município não encontrado.")) : city;
    }

    /// <summary>
    /// Série semanal de alertas de dengue, chikungunya e zika. Na primeira consulta de uma cidade os dados são
    /// buscados na fonte externa (pode levar alguns segundos).
    /// </summary>
    [HttpGet("{geocode:int}/alerts")]
    public async Task<ActionResult<CityAlertsResponse>> GetAlerts(
        int geocode,
        [FromQuery] Disease? disease,
        [FromQuery] int weeks = 12,
        CancellationToken cancellationToken = default)
    {
        if (weeks is < 1 or > MaxWeeks) return BadRequest(Problem($"weeks deve estar entre 1 e {MaxWeeks}."));

        return ToActionResult(await alerts.GetAlertsAsync(geocode, disease, weeks, cancellationToken));
    }

    /// <summary>Semana mais recente disponível de cada doença.</summary>
    [HttpGet("{geocode:int}/alerts/latest")]
    public async Task<ActionResult<CityAlertsResponse>> GetLatest(int geocode, CancellationToken cancellationToken)
    {
        return ToActionResult(await alerts.GetLatestAsync(geocode, cancellationToken));
    }

    private ActionResult<CityAlertsResponse> ToActionResult(AlertsOutcome outcome) => outcome.Status switch
    {
        AlertsStatus.Ok => outcome.Response!,
        AlertsStatus.CityNotFound => NotFound(Problem("Município não encontrado.")),
        _ => StatusCode(StatusCodes.Status503ServiceUnavailable,
            Problem("Fonte de dados externa indisponível no momento. Tente novamente em instantes.",
                StatusCodes.Status503ServiceUnavailable)),
    };

    private static ProblemDetails Problem(string detail, int status = StatusCodes.Status400BadRequest) =>
        new() { Status = status, Detail = detail };
}
