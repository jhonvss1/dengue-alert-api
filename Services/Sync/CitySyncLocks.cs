using System.Collections.Concurrent;

namespace ApiAlertaDengue.Services.Sync;

/// <summary>Garante uma sincronização por vez para cada cidade (evita buscas duplicadas na fonte).</summary>
public class CitySyncLocks
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(int geocode, CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(geocode, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
