using ApiAlertaDengue.Data;
using ApiAlertaDengue.Models;
using ApiAlertaDengue.Services.Alerts;
using ApiAlertaDengue.Services.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Tests;

public class CityAlertsServiceTests
{
    private const int Bh = 3106200;
    private static readonly DateTimeOffset Today = new(2025, 3, 5, 12, 0, 0, TimeSpan.Zero); // semana epi 2025/10

    private static (CityAlertsService Service, AppDbContext Db) Create(
        TestDb testDb, FakeClient client, FakeTimeProvider time)
    {
        var db = testDb.CreateContext();
        var options = Options.Create(new SyncOptions());
        var sync = new CitySyncService(db, client, new CitySyncLocks(), options, time, NullLogger<CitySyncService>.Instance);
        return (new CityAlertsService(db, sync, options, time, NullLogger<CityAlertsService>.Instance), db);
    }

    private static async Task SeedAsync(TestDb testDb, DateTime? lastSyncedAt = null, params DengueAlert[] alerts)
    {
        await using var db = testDb.CreateContext();
        db.Cities.AddRange(
            new City { Geocode = Bh, Name = "Belo Horizonte", State = "MG", LastSyncedAt = lastSyncedAt, IsTracked = lastSyncedAt != null },
            new City { Geocode = 3550308, Name = "São Paulo", State = "SP" },
            new City { Geocode = 3304557, Name = "Rio de Janeiro", State = "RJ" },
            new City { Geocode = 3106705, Name = "Betim", State = "MG" });
        db.DengueAlerts.AddRange(alerts);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAlerts_OnFirstQuery_FetchesFromSourceAndReturnsData()
    {
        using var testDb = new TestDb();
        await SeedAsync(testDb);
        var client = new FakeClient { Respond = d => [Fakes.Alert(Bh, d, 10, 100), Fakes.Alert(Bh, d, 9, 90)] };
        var (service, db) = Create(testDb, client, new FakeTimeProvider(Today));
        await using var _ = db;

        var outcome = await service.GetAlertsAsync(Bh, null, 12);

        Assert.Equal(AlertsStatus.Ok, outcome.Status);
        Assert.Equal(3, client.Calls.Count);
        Assert.Equal(6, outcome.Response!.Alerts.Count);
        Assert.False(outcome.Response.IsStale);
        Assert.Equal(Today.UtcDateTime, outcome.Response.LastSyncedAt);
        Assert.True(outcome.Response.City.IsTracked);
        Assert.All(outcome.Response.Alerts.Take(3), a => Assert.Equal(10, a.Week)); // mais recente primeiro
    }

    [Fact]
    public async Task GetAlerts_WithFreshData_DoesNotCallSource_AndFiltersByDiseaseAndWeeks()
    {
        using var testDb = new TestDb();
        await SeedAsync(testDb, Today.UtcDateTime.AddDays(-1),
            Fakes.Alert(Bh, Disease.Dengue, 10, 5), Fakes.Alert(Bh, Disease.Dengue, 5, 5),
            Fakes.Alert(Bh, Disease.Zika, 10, 1));
        var client = new FakeClient();
        var (service, db) = Create(testDb, client, new FakeTimeProvider(Today));
        await using var _ = db;

        var outcome = await service.GetAlertsAsync(Bh, Disease.Dengue, 3);

        Assert.Empty(client.Calls);
        var alert = Assert.Single(outcome.Response!.Alerts); // a semana 5 fica fora das últimas 3 semanas
        Assert.Equal("dengue", alert.Disease);
        Assert.Equal(10, alert.Week);
    }

    [Fact]
    public async Task GetAlerts_WhenSourceFailsOnFirstQuery_ReturnsSourceUnavailable()
    {
        using var testDb = new TestDb();
        await SeedAsync(testDb);
        var (service, db) = Create(testDb, new FakeClient { FailAll = true }, new FakeTimeProvider(Today));
        await using var _ = db;

        var outcome = await service.GetAlertsAsync(Bh, null, 12);

        Assert.Equal(AlertsStatus.SourceUnavailable, outcome.Status);
    }

    [Fact]
    public async Task GetAlerts_WhenStaleAndSourceFails_ServesSavedDataMarkedAsStale()
    {
        using var testDb = new TestDb();
        var lastSync = Today.UtcDateTime.AddDays(-30);
        await SeedAsync(testDb, lastSync, Fakes.Alert(Bh, Disease.Dengue, 10, 5));
        var (service, db) = Create(testDb, new FakeClient { FailAll = true }, new FakeTimeProvider(Today));
        await using var _ = db;

        var outcome = await service.GetAlertsAsync(Bh, null, 12);

        Assert.Equal(AlertsStatus.Ok, outcome.Status);
        Assert.True(outcome.Response!.IsStale);
        Assert.Equal(lastSync, outcome.Response.LastSyncedAt);
        Assert.Single(outcome.Response.Alerts);
    }

    [Fact]
    public async Task GetAlerts_UnknownCity_ReturnsNotFound()
    {
        using var testDb = new TestDb();
        await SeedAsync(testDb);
        var (service, db) = Create(testDb, new FakeClient(), new FakeTimeProvider(Today));
        await using var _ = db;

        Assert.Equal(AlertsStatus.CityNotFound, (await service.GetAlertsAsync(1, null, 12)).Status);
        Assert.Equal(AlertsStatus.CityNotFound, (await service.GetLatestAsync(1)).Status);
    }

    [Fact]
    public async Task GetLatest_ReturnsMostRecentWeekOfEachDisease()
    {
        using var testDb = new TestDb();
        await SeedAsync(testDb, Today.UtcDateTime,
            Fakes.Alert(Bh, Disease.Dengue, 10, 5), Fakes.Alert(Bh, Disease.Dengue, 9, 4),
            Fakes.Alert(Bh, Disease.Zika, 8, 1), Fakes.Alert(Bh, Disease.Zika, 7, 1));
        var (service, db) = Create(testDb, new FakeClient(), new FakeTimeProvider(Today));
        await using var _ = db;

        var outcome = await service.GetLatestAsync(Bh);

        Assert.Equal([("dengue", 10), ("zika", 8)], outcome.Response!.Alerts.Select(a => (a.Disease, a.Week)));
    }

    [Fact]
    public async Task SearchCities_FiltersByNameAndState_AndPaginates()
    {
        using var testDb = new TestDb();
        await SeedAsync(testDb);
        var (service, db) = Create(testDb, new FakeClient(), new FakeTimeProvider(Today));
        await using var _ = db;

        var byName = await service.SearchCitiesAsync("be", null, 1, 10);
        Assert.Equal(["Belo Horizonte", "Betim"], byName.Items.Select(c => c.Name));

        var byState = await service.SearchCitiesAsync(null, "mg", 1, 10);
        Assert.Equal(2, byState.Total);

        var page2 = await service.SearchCitiesAsync(null, null, 2, 3);
        Assert.Equal(4, page2.Total);
        Assert.Single(page2.Items);
        Assert.Equal("São Paulo", page2.Items[0].Name);
    }
}
