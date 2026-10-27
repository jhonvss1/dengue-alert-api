using ApiAlertaDengue.Data;
using ApiAlertaDengue.Models;
using ApiAlertaDengue.Services.InfoDengue;
using ApiAlertaDengue.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Tests;

public class SyncRunnerTests
{
    private static readonly DateTimeOffset Today = new(2025, 3, 5, 12, 0, 0, TimeSpan.Zero);

    private class FakeLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }

    private static (SyncRunner Runner, FakeClient Client) Create(TestDb testDb, FakeClient? client = null, int maxConcurrency = 2)
    {
        client ??= new FakeClient { Respond = d => [Fakes.Alert(1, d, 10, 1)] };
        var time = new FakeTimeProvider(Today);
        var syncOptions = Options.Create(new SyncOptions { DelayBetweenCitiesMs = 0, MaxConcurrency = maxConcurrency });

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(testDb.Connection));
        services.AddSingleton<IInfoDengueClient>(client);
        services.AddSingleton(new CitySyncLocks());
        services.AddSingleton(syncOptions);
        services.AddSingleton<TimeProvider>(time);
        services.AddLogging();
        services.AddScoped<ICitySyncService, CitySyncService>();
        var provider = services.BuildServiceProvider();

        var runner = new SyncRunner(provider.GetRequiredService<IServiceScopeFactory>(), syncOptions, time,
            new FakeLifetime(), NullLogger<SyncRunner>.Instance);
        return (runner, client);
    }

    private static async Task SeedAsync(TestDb testDb)
    {
        await using var db = testDb.CreateContext();
        db.Cities.AddRange(
            new City { Geocode = 1, Name = "A", State = "MG", IsTracked = true, LastSyncedAt = Today.UtcDateTime.AddDays(-9) },
            new City { Geocode = 2, Name = "B", State = "MG", IsTracked = true, LastSyncedAt = Today.UtcDateTime.AddDays(-10) },
            new City { Geocode = 3, Name = "C", State = "SP" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Start_WithoutGeocodes_SyncsOnlyTrackedCities_OldestFirst_AndRecordsRun()
    {
        using var testDb = new TestDb();
        await SeedAsync(testDb);
        var (runner, client) = Create(testDb, maxConcurrency: 1);

        var started = await runner.StartAsync("scheduled");
        Assert.NotNull(started);
        await started.Completion;

        Assert.Equal([2, 1], client.Calls.Select(c => c.Geocode).Distinct());

        await using var db = testDb.CreateContext();
        var run = await db.SyncRuns.AsNoTracking().SingleAsync();
        Assert.Equal("scheduled", run.Trigger);
        Assert.Equal(2, run.CitiesTotal);
        Assert.Equal(2, run.CitiesSucceeded);
        Assert.Equal(0, run.CitiesFailed);
        Assert.NotNull(run.FinishedAt);
        Assert.Null(run.Error);
    }

    [Fact]
    public async Task Start_WithGeocodes_SyncsAndTracksThoseCities()
    {
        using var testDb = new TestDb();
        await SeedAsync(testDb);
        var (runner, _) = Create(testDb);

        var started = await runner.StartAsync("manual", [3]);
        await started!.Completion;

        await using var db = testDb.CreateContext();
        Assert.True((await db.Cities.AsNoTracking().SingleAsync(c => c.Geocode == 3)).IsTracked);
        Assert.Equal(1, (await db.SyncRuns.AsNoTracking().SingleAsync()).CitiesTotal);
    }

    [Fact]
    public async Task Start_WhileRunning_ReturnsNull()
    {
        using var testDb = new TestDb();
        await SeedAsync(testDb);
        var (runner, _) = Create(testDb, new FakeClient { Latency = TimeSpan.FromMilliseconds(150) });

        var first = await runner.StartAsync("scheduled");
        var second = await runner.StartAsync("manual");

        Assert.NotNull(first);
        Assert.Null(second);

        await first.Completion;
        Assert.NotNull(await runner.StartAsync("manual")); // liberado após o fim
    }

    [Fact]
    public async Task FailingCities_AreCountedAndDoNotStopTheRun()
    {
        using var testDb = new TestDb();
        await SeedAsync(testDb);
        var (runner, _) = Create(testDb, new FakeClient { FailAll = true });

        var started = await runner.StartAsync("scheduled");
        await started!.Completion;

        await using var db = testDb.CreateContext();
        var run = await db.SyncRuns.AsNoTracking().SingleAsync();
        Assert.Equal(0, run.CitiesSucceeded);
        Assert.Equal(2, run.CitiesFailed);
        Assert.Contains("2 cidade(s) falharam", run.Error);
        Assert.NotNull(run.FinishedAt);
    }
}
