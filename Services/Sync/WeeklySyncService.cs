using ApiAlertaDengue.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Services.Sync;

/// <summary>
/// Atualiza semanalmente as cidades acompanhadas. Se a API ficou fora do ar no horário agendado,
/// recupera a rodada perdida assim que subir.
/// </summary>
public class WeeklySyncService(
    IServiceScopeFactory scopeFactory,
    ISyncRunner runner,
    IOptions<SyncOptions> options,
    TimeProvider timeProvider,
    ILogger<WeeklySyncService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Sincronização semanal desabilitada (Sync:Enabled=false).");
            return;
        }

        var timeZone = WeeklySchedule.ResolveTimeZone(settings.TimeZone, logger);

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);

            var next = WeeklySchedule.NextOccurrence(timeProvider.GetUtcNow(), settings, timeZone);
            if (await MissedLastSlotAsync(next, stoppingToken))
            {
                logger.LogInformation("Rodada semanal perdida detectada; sincronizando agora.");
                await RunAsync();
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                next = WeeklySchedule.NextOccurrence(timeProvider.GetUtcNow(), settings, timeZone);
                logger.LogInformation("Próxima sincronização semanal: {Next:u}.", next);

                await Task.Delay(next - timeProvider.GetUtcNow(), stoppingToken);
                await RunAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // encerrando
        }
    }

    private async Task RunAsync()
    {
        var started = await runner.StartAsync("scheduled");
        if (started is null)
        {
            logger.LogInformation("Já existe uma rodada em andamento; rodada agendada ignorada.");
            return;
        }

        await started.Completion;
    }

    /// <summary>Houve acompanhamento a atualizar e nenhuma rodada começou desde o último horário agendado?</summary>
    private async Task<bool> MissedLastSlotAsync(DateTimeOffset nextSlot, CancellationToken cancellationToken)
    {
        var previousSlot = nextSlot.UtcDateTime.AddDays(-7);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.Cities.AnyAsync(c => c.IsTracked, cancellationToken)) return false;

        var lastStart = await db.SyncRuns.MaxAsync(r => (DateTime?)r.StartedAt, cancellationToken);
        return lastStart is null || lastStart < previousSlot;
    }
}
