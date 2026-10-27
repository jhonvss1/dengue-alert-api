using ApiAlertaDengue.Helpers;
using ApiAlertaDengue.Models;
using ApiAlertaDengue.Services.InfoDengue;

namespace ApiAlertaDengue.Tests;

public class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

public class FakeClient : IInfoDengueClient
{
    public List<(int Geocode, Disease Disease, EpidemiologicalWeek.Week From, EpidemiologicalWeek.Week To)> Calls { get; } = [];
    public Func<Disease, IReadOnlyList<DengueAlert>> Respond { get; set; } = _ => [];
    public Disease? FailOn { get; set; }
    public bool FailAll { get; set; }
    public TimeSpan Latency { get; set; }

    public async Task<IReadOnlyList<DengueAlert>> GetAlertsAsync(
        int geocode, Disease disease, EpidemiologicalWeek.Week from, EpidemiologicalWeek.Week to, CancellationToken ct = default)
    {
        lock (Calls) Calls.Add((geocode, disease, from, to));
        if (Latency > TimeSpan.Zero) await Task.Delay(Latency, ct);
        if (FailAll || disease == FailOn) throw new InfoDengueException("falhou");
        return Respond(disease);
    }
}

public static class Fakes
{
    public static DengueAlert Alert(
        int geocode, Disease disease, int week, double estimated, int reported = 10, AlertLevel level = AlertLevel.Green) => new()
    {
        Geocode = geocode, Disease = disease, Year = 2025, Week = week,
        EstimatedCases = estimated, ReportedCases = reported, AlertLevel = level,
    };
}
