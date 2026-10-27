namespace ApiAlertaDengue.Models;

/// <summary>Registro de uma rodada de sincronização semanal.</summary>
public class SyncRun
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    /// <summary>"scheduled" ou "manual".</summary>
    public string Trigger { get; set; } = "scheduled";

    public int CitiesTotal { get; set; }
    public int CitiesSucceeded { get; set; }
    public int CitiesFailed { get; set; }
    public string? Error { get; set; }
}
