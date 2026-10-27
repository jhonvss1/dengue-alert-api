using ApiAlertaDengue.Data;
using ApiAlertaDengue.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Services.Sync;

public class SyncRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<SyncOptions> options,
    TimeProvider timeProvider,
    IHostApplicationLifetime lifetime,
    ILogger<SyncRunner> logger) : ISyncRunner
{
    private readonly SemaphoreSlim _running = new(1, 1);

    public async Task<StartedSyncRun?> StartAsync(string trigger, IReadOnlyCollection<int>? geocodes = null)
    {
        if (!await _running.WaitAsync(0)) return null;

        try
        {
            List<int> targets;
            SyncRun run;
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Mais antigas primeiro: se a rodada for interrompida, a próxima continua de onde parou.
                targets = await db.Cities.AsNoTracking()
                    .Where(c => geocodes == null ? c.IsTracked : geocodes.Contains(c.Geocode))
                    .OrderBy(c => c.LastSyncedAt)
                    .Select(c => c.Geocode)
                    .ToListAsync();

                run = new SyncRun
                {
                    Trigger = trigger,
                    StartedAt = timeProvider.GetUtcNow().UtcDateTime,
                    CitiesTotal = targets.Count,
                };
                db.SyncRuns.Add(run);
                await db.SaveChangesAsync();
            }

            var completion = Task.Run(() => ProcessAsync(run, targets));
            return new StartedSyncRun(run, completion);
        }
        catch
        {
            _running.Release();
            throw;
        }
    }

    private async Task ProcessAsync(SyncRun run, List<int> targets)
    {
        var settings = options.Value;
        var succeeded = 0;
        var failed = 0;
        string? firstError = null;
        string? abortError = null;

        logger.LogInformation("Rodada de sincronização {RunId} ({Trigger}) iniciada: {Total} cidades.",
            run.Id, run.Trigger, targets.Count);

        try
        {
            await Parallel.ForEachAsync(
                targets,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, settings.MaxConcurrency),
                    CancellationToken = lifetime.ApplicationStopping,
                },
                async (geocode, ct) =>
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var sync = scope.ServiceProvider.GetRequiredService<ICitySyncService>();
                        var result = await sync.SyncCityAsync(geocode, SyncMode.Force, ct);
                        if (result.Status == CitySyncStatus.CityNotFound) throw new InvalidOperationException("Cidade não encontrada.");
                        Interlocked.Increment(ref succeeded);
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        Interlocked.Increment(ref failed);
                        Interlocked.CompareExchange(ref firstError, $"{geocode}: {ex.Message}", null);
                        logger.LogWarning(ex, "Falha ao sincronizar a cidade {Geocode}.", geocode);
                    }

                    if (settings.DelayBetweenCitiesMs > 0)
                        await Task.Delay(settings.DelayBetweenCitiesMs, ct);
                });
        }
        catch (OperationCanceledException)
        {
            abortError = "Interrompida (aplicação encerrando).";
        }
        catch (Exception ex)
        {
            abortError = ex.Message;
            logger.LogError(ex, "Rodada de sincronização {RunId} falhou.", run.Id);
        }
        finally
        {
            await FinishAsync(run.Id, succeeded, failed, abortError ?? (failed > 0 ? $"{failed} cidade(s) falharam. Primeira: {firstError}" : null));
            _running.Release();
        }
    }

    private async Task FinishAsync(int runId, int succeeded, int failed, string? error)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = await db.SyncRuns.FindAsync(runId);
            if (run is null) return;

            run.FinishedAt = timeProvider.GetUtcNow().UtcDateTime;
            run.CitiesSucceeded = succeeded;
            run.CitiesFailed = failed;
            run.Error = error is { Length: > 2000 } ? error[..2000] : error;
            await db.SaveChangesAsync();

            logger.LogInformation("Rodada de sincronização {RunId} concluída: {Ok} ok, {Failed} falhas.", runId, succeeded, failed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Não foi possível registrar o fim da rodada {RunId}.", runId);
        }
    }
}
