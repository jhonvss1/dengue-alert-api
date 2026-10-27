namespace ApiAlertaDengue.Contracts;

public record SyncRequest(List<int>? Geocodes);

public record SyncRunDto(
    int Id,
    string Trigger,
    DateTime StartedAt,
    DateTime? FinishedAt,
    int CitiesTotal,
    int CitiesSucceeded,
    int CitiesFailed,
    string? Error);
