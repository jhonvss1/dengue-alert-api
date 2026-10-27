using ApiAlertaDengue.Models;

namespace ApiAlertaDengue.Services.Sync;

public record StartedSyncRun(SyncRun Run, Task Completion);

public interface ISyncRunner
{
    /// <summary>
    /// Inicia uma rodada de sincronização (todas as cidades acompanhadas, ou só as informadas).
    /// Retorna null se já houver uma rodada em andamento. A rodada continua em segundo plano; use
    /// <see cref="StartedSyncRun.Completion"/> para aguardá-la.
    /// </summary>
    Task<StartedSyncRun?> StartAsync(string trigger, IReadOnlyCollection<int>? geocodes = null);
}
