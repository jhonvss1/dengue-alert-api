using ApiAlertaDengue.Data;
using Microsoft.EntityFrameworkCore;

namespace ApiAlertaDengue.Services.Cities;

/// <summary>Na primeira execução (tabela vazia) carrega o cadastro de municípios do IBGE, sem travar a subida da API.</summary>
public class CityCatalogSeeder(IServiceScopeFactory scopeFactory, ILogger<CityCatalogSeeder> logger) : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 0; !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (await db.Cities.AnyAsync(stoppingToken)) return;

                await scope.ServiceProvider.GetRequiredService<ICityCatalogService>().RefreshAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
                logger.LogWarning(ex, "Falha ao carregar municípios do IBGE. Nova tentativa em {Delay}.", delay);
                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
