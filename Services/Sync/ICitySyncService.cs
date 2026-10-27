namespace ApiAlertaDengue.Services.Sync;

public enum SyncMode
{
    /// <summary>Só busca se a cidade nunca foi sincronizada ou se o dado está velho.</summary>
    IfNeeded,

    /// <summary>Busca sempre (job semanal e sincronização manual).</summary>
    Force,
}

public enum CitySyncStatus
{
    Synced,
    Skipped,
    CityNotFound,
}

public record CitySyncResult(CitySyncStatus Status, int AlertsWritten = 0);

public interface ICitySyncService
{
    /// <summary>
    /// Sincroniza as três doenças de uma cidade e a marca como acompanhada. É tudo ou nada: se a fonte falhar
    /// em qualquer doença, nada é gravado e <see cref="InfoDengue.InfoDengueException"/> é lançada.
    /// </summary>
    Task<CitySyncResult> SyncCityAsync(int geocode, SyncMode mode, CancellationToken cancellationToken = default);
}
