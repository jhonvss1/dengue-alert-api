using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Services.Sync;

/// <summary>Limita, em janela deslizante de 1 hora, quantas cidades novas são buscadas na fonte sob demanda.</summary>
public class OnDemandSyncLimiter(IOptions<SyncOptions> options, TimeProvider timeProvider)
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly Queue<DateTimeOffset> _starts = new();
    private readonly object _gate = new();

    /// <summary>Reserva uma vaga. Se o teto foi atingido, informa em quanto tempo uma vaga libera.</summary>
    public bool TryAcquire(out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        var max = options.Value.MaxNewCitiesPerHour;
        if (max <= 0) return true;

        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            while (_starts.Count > 0 && now - _starts.Peek() >= Window) _starts.Dequeue();

            if (_starts.Count >= max)
            {
                retryAfter = _starts.Peek() + Window - now;
                return false;
            }

            _starts.Enqueue(now);
            return true;
        }
    }
}

public class SyncRateLimitedException(TimeSpan retryAfter)
    : Exception($"Limite de cidades novas por hora atingido. Tente novamente em {Math.Ceiling(retryAfter.TotalMinutes)} min.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
