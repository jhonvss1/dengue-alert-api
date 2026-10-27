using System.Text.Json;
using ApiAlertaDengue.Contracts;
using ApiAlertaDengue.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiAlertaDengue.Tests;

public class UtcDateTimeTests
{
    [Fact]
    public async Task DateTimes_AreReadAsUtc_AndSerializedWithZ()
    {
        using var testDb = new TestDb();
        var synced = new DateTime(2025, 3, 10, 6, 0, 12, DateTimeKind.Unspecified);
        await using (var db = testDb.CreateContext())
        {
            db.Cities.Add(new City { Geocode = 1, Name = "A", State = "MG", LastSyncedAt = synced });
            await db.SaveChangesAsync();
        }

        await using var read = testDb.CreateContext();
        var city = await read.Cities.AsNoTracking().SingleAsync();

        Assert.Equal(DateTimeKind.Utc, city.LastSyncedAt!.Value.Kind);
        var json = JsonSerializer.Serialize(CityDto.From(city));
        Assert.Contains("2025-03-10T06:00:12Z", json);
    }

    [Fact]
    public async Task LocalDateTimes_AreConvertedToUtcOnWrite()
    {
        using var testDb = new TestDb();
        var local = new DateTime(2025, 3, 10, 12, 0, 0, DateTimeKind.Utc).ToLocalTime();
        await using (var db = testDb.CreateContext())
        {
            db.SyncRuns.Add(new SyncRun { StartedAt = local });
            await db.SaveChangesAsync();
        }

        await using var read = testDb.CreateContext();
        var run = await read.SyncRuns.AsNoTracking().SingleAsync();

        Assert.Equal(new DateTime(2025, 3, 10, 12, 0, 0, DateTimeKind.Utc), run.StartedAt);
    }
}
