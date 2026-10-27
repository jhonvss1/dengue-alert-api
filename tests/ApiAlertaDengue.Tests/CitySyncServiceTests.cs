using ApiAlertaDengue.Data;
using ApiAlertaDengue.Helpers;
using ApiAlertaDengue.Models;
using ApiAlertaDengue.Services.InfoDengue;
using ApiAlertaDengue.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Tests;

public class CitySyncServiceTests
{
    private const int Bh = 3106200;
    private static readonly DateTimeOffset Today = new(2025, 3, 5, 12, 0, 0, TimeSpan.Zero); // semana epi 2025/10

    private static DengueAlert Alert(Disease disease, int week, double estimated, int reported = 10, AlertLevel level = AlertLevel.Green) =>
        Fakes.Alert(Bh, disease, week, estimated, reported, level);

    private static CitySyncService CreateService(
        AppDbContext db, FakeClient client, FakeTimeProvider time, CitySyncLocks? locks = null) =>
        new(db, client, locks ?? new CitySyncLocks(), Options.Create(new SyncOptions()), time,
            NullLogger<CitySyncService>.Instance);

    private static async Task SeedCityAsync(TestDb testDb, DateTime? lastSyncedAt = null)
    {
        await using var db = testDb.CreateContext();
        db.Cities.Add(new City
        {
            Geocode = Bh, Name = "Belo Horizonte", State = "MG",
            IsTracked = lastSyncedAt is not null, LastSyncedAt = lastSyncedAt,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task FirstSync_FetchesAllDiseasesWithFullHistory_AndTracksCity()
    {
        using var testDb = new TestDb();
        await SeedCityAsync(testDb);
        var client = new FakeClient { Respond = d => [Alert(d, 9, 100), Alert(d, 10, 200)] };
        var time = new FakeTimeProvider(Today);

        await using var db = testDb.CreateContext();
        var result = await CreateService(db, client, time).SyncCityAsync(Bh, SyncMode.IfNeeded);

        Assert.Equal(CitySyncStatus.Synced, result.Status);
        Assert.Equal(6, result.AlertsWritten);
        Assert.Equal(3, client.Calls.Count);
        Assert.Equal([Disease.Dengue, Disease.Chikungunya, Disease.Zika], client.Calls.Select(c => c.Disease));

        var expectedFrom = EpidemiologicalWeek.Subtract(new EpidemiologicalWeek.Week(2025, 10), 52);
        Assert.All(client.Calls, c =>
        {
            Assert.Equal(expectedFrom, c.From);
            Assert.Equal(new EpidemiologicalWeek.Week(2025, 10), c.To);
        });

        await using var verify = testDb.CreateContext();
        var city = await verify.Cities.AsNoTracking().SingleAsync();
        Assert.True(city.IsTracked);
        Assert.Equal(Today.UtcDateTime, city.LastSyncedAt);
        Assert.Equal(6, await verify.DengueAlerts.CountAsync());
    }

    [Fact]
    public async Task ForcedSync_UpdatesChangedRows_WithoutDuplicating()
    {
        using var testDb = new TestDb();
        await SeedCityAsync(testDb);
        var client = new FakeClient { Respond = d => [Alert(d, 9, 100), Alert(d, 10, 200)] };
        var time = new FakeTimeProvider(Today);

        await using (var db = testDb.CreateContext())
            await CreateService(db, client, time).SyncCityAsync(Bh, SyncMode.Force);

        // A fonte revisa a semana 10 da dengue e acrescenta a semana 11.
        client.Respond = d => d == Disease.Dengue
            ? [Alert(d, 9, 100), Alert(d, 10, 260, level: AlertLevel.Orange), Alert(d, 11, 30)]
            : [Alert(d, 9, 100), Alert(d, 10, 200)];
        time.Now = Today.AddDays(7);

        await using (var db = testDb.CreateContext())
        {
            var result = await CreateService(db, client, time).SyncCityAsync(Bh, SyncMode.Force);
            Assert.Equal(2, result.AlertsWritten); // 1 atualizado + 1 novo
        }

        await using var verify = testDb.CreateContext();
        Assert.Equal(7, await verify.DengueAlerts.CountAsync());

        var revised = await verify.DengueAlerts.SingleAsync(a => a.Disease == Disease.Dengue && a.Week == 10);
        Assert.Equal(260, revised.EstimatedCases);
        Assert.Equal(AlertLevel.Orange, revised.AlertLevel);
        Assert.Equal(Today.AddDays(7).UtcDateTime, revised.UpdatedAt);

        var untouched = await verify.DengueAlerts.SingleAsync(a => a.Disease == Disease.Zika && a.Week == 10);
        Assert.Equal(Today.UtcDateTime, untouched.UpdatedAt);
    }

    [Fact]
    public async Task SecondSync_OnlyFetchesSinceLastSyncPlusRefreshWindow()
    {
        using var testDb = new TestDb();
        await SeedCityAsync(testDb, lastSyncedAt: Today.UtcDateTime.AddDays(-7));
        var client = new FakeClient();

        await using var db = testDb.CreateContext();
        await CreateService(db, client, new FakeTimeProvider(Today)).SyncCityAsync(Bh, SyncMode.Force);

        // 1 semana desde a última sincronização + 8 semanas de janela de revisão
        var expectedFrom = EpidemiologicalWeek.Subtract(new EpidemiologicalWeek.Week(2025, 10), 9);
        Assert.All(client.Calls, c => Assert.Equal(expectedFrom, c.From));
    }

    [Fact]
    public async Task IfNeeded_SkipsFreshCity_AndRefreshesStaleCity()
    {
        using var testDb = new TestDb();
        await SeedCityAsync(testDb, lastSyncedAt: Today.UtcDateTime.AddDays(-3));
        var client = new FakeClient();
        var time = new FakeTimeProvider(Today);

        await using (var db = testDb.CreateContext())
        {
            var fresh = await CreateService(db, client, time).SyncCityAsync(Bh, SyncMode.IfNeeded);
            Assert.Equal(CitySyncStatus.Skipped, fresh.Status);
            Assert.Empty(client.Calls);
        }

        time.Now = Today.AddDays(30);
        await using (var db = testDb.CreateContext())
        {
            var stale = await CreateService(db, client, time).SyncCityAsync(Bh, SyncMode.IfNeeded);
            Assert.Equal(CitySyncStatus.Synced, stale.Status);
            Assert.Equal(3, client.Calls.Count);
        }
    }

    [Fact]
    public async Task Failure_OnAnyDisease_SavesNothing_AndDoesNotTrackCity()
    {
        using var testDb = new TestDb();
        await SeedCityAsync(testDb);
        var client = new FakeClient { Respond = d => [Alert(d, 10, 1)], FailOn = Disease.Zika };

        await using (var db = testDb.CreateContext())
        {
            await Assert.ThrowsAsync<InfoDengueException>(
                () => CreateService(db, client, new FakeTimeProvider(Today)).SyncCityAsync(Bh, SyncMode.IfNeeded));
        }

        await using var verify = testDb.CreateContext();
        Assert.Empty(await verify.DengueAlerts.ToListAsync());
        var city = await verify.Cities.AsNoTracking().SingleAsync();
        Assert.False(city.IsTracked);
        Assert.Null(city.LastSyncedAt);
    }

    [Fact]
    public async Task UnknownCity_ReturnsNotFound()
    {
        using var testDb = new TestDb();
        await using var db = testDb.CreateContext();

        var result = await CreateService(db, new FakeClient(), new FakeTimeProvider(Today)).SyncCityAsync(999, SyncMode.IfNeeded);

        Assert.Equal(CitySyncStatus.CityNotFound, result.Status);
    }

    [Fact]
    public async Task ConcurrentFirstRequests_FetchFromSourceOnlyOnce()
    {
        using var testDb = new TestDb();
        await SeedCityAsync(testDb);
        var client = new FakeClient { Respond = d => [Alert(d, 10, 1)], Latency = TimeSpan.FromMilliseconds(50) };
        var time = new FakeTimeProvider(Today);
        var locks = new CitySyncLocks();

        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var db = testDb.CreateContext();
            return await CreateService(db, client, time, locks).SyncCityAsync(Bh, SyncMode.IfNeeded);
        });
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r.Status == CitySyncStatus.Synced));
        Assert.Equal(4, results.Count(r => r.Status == CitySyncStatus.Skipped));
        Assert.Equal(3, client.Calls.Count);
    }

    [Theory]
    [InlineData(null, 52)]
    [InlineData(0, 8)]     // sincronizado agora: só a janela de revisão
    [InlineData(7, 9)]
    [InlineData(70, 18)]   // 10 semanas + janela de revisão
    [InlineData(400, 52)]  // muito tempo parado: limitado ao histórico inicial
    public void WeeksToFetch_ScalesWithTimeSinceLastSync(int? daysAgo, int expected)
    {
        var now = Today.UtcDateTime;
        DateTime? last = daysAgo is { } d ? now.AddDays(-d) : null;

        Assert.Equal(expected, CitySyncService.WeeksToFetch(last, now, new SyncOptions()));
    }
}
