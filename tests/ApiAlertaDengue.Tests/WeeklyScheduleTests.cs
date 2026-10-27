using ApiAlertaDengue.Services.Sync;

namespace ApiAlertaDengue.Tests;

public class WeeklyScheduleTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly SyncOptions Monday3Am = new() { DayOfWeek = DayOfWeek.Monday, TimeOfDay = "03:00" };

    [Theory]
    [InlineData("2025-03-05T12:00:00Z", "2025-03-10T06:00:00Z")] // quarta -> segunda seguinte, 03:00 BRT = 06:00 UTC
    [InlineData("2025-03-10T05:59:59Z", "2025-03-10T06:00:00Z")] // segunda, um segundo antes
    [InlineData("2025-03-10T06:00:00Z", "2025-03-17T06:00:00Z")] // exatamente no horário: próxima semana
    [InlineData("2025-03-10T20:00:00Z", "2025-03-17T06:00:00Z")] // segunda, depois do horário
    public void NextOccurrence_ReturnsNextConfiguredSlot(string now, string expected)
    {
        var next = WeeklySchedule.NextOccurrence(DateTimeOffset.Parse(now), Monday3Am, SaoPaulo);

        Assert.Equal(DateTimeOffset.Parse(expected), next);
    }

    [Fact]
    public void NextOccurrence_WorksInUtc()
    {
        var next = WeeklySchedule.NextOccurrence(
            DateTimeOffset.Parse("2025-03-05T12:00:00Z"),
            new SyncOptions { DayOfWeek = DayOfWeek.Wednesday, TimeOfDay = "18:30" },
            TimeZoneInfo.Utc);

        Assert.Equal(DateTimeOffset.Parse("2025-03-05T18:30:00Z"), next);
    }
}
