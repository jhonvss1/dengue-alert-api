using ApiAlertaDengue.Contracts;
using ApiAlertaDengue.Data;
using ApiAlertaDengue.Security;
using ApiAlertaDengue.Services.Cities;
using ApiAlertaDengue.Services.Sync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiAlertaDengue.Controllers;

/// <summary>Operações de manutenção. Exigem o header X-Api-Key (configuração Admin:ApiKey).</summary>
[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(AdminApiKeyFilter))]
public class AdminController(AppDbContext db, ISyncRunner runner, ICityCatalogService catalog) : ControllerBase
{
    /// <summary>
    /// Dispara uma rodada de sincronização em segundo plano: todas as cidades acompanhadas ou, se informado,
    /// só as cidades listadas (que passam a ser acompanhadas — útil para pré-carregar capitais).
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> StartSync([FromBody] SyncRequest? request, CancellationToken cancellationToken)
    {
        var geocodes = request?.Geocodes?.Distinct().ToList();

        if (geocodes is { Count: > 0 })
        {
            var known = await db.Cities.Where(c => geocodes.Contains(c.Geocode)).Select(c => c.Geocode)
                .ToListAsync(cancellationToken);
            var unknown = geocodes.Except(known).ToList();
            if (unknown.Count > 0)
                return BadRequest(new ProblemDetails { Status = 400, Detail = $"Municípios desconhecidos: {string.Join(", ", unknown)}." });
        }

        var started = await runner.StartAsync("manual", geocodes is { Count: > 0 } ? geocodes : null);
        if (started is null)
            return Conflict(new ProblemDetails { Status = 409, Detail = "Já existe uma sincronização em andamento." });

        return Accepted(new { runId = started.Run.Id, cities = started.Run.CitiesTotal });
    }

    /// <summary>Últimas rodadas de sincronização.</summary>
    [HttpGet("sync/runs")]
    public async Task<IReadOnlyList<SyncRunDto>> GetRuns(CancellationToken cancellationToken)
    {
        var runs = await db.SyncRuns.AsNoTracking()
            .OrderByDescending(r => r.StartedAt).Take(20)
            .ToListAsync(cancellationToken);

        return runs.Select(r => new SyncRunDto(r.Id, r.Trigger, r.StartedAt, r.FinishedAt,
            r.CitiesTotal, r.CitiesSucceeded, r.CitiesFailed, r.Error)).ToList();
    }

    /// <summary>Recarrega o cadastro de municípios a partir do IBGE.</summary>
    [HttpPost("cities/refresh")]
    public async Task<IActionResult> RefreshCities(CancellationToken cancellationToken)
    {
        var inserted = await catalog.RefreshAsync(cancellationToken);
        return Ok(new { inserted });
    }
}
