using ApiAlertaDengue.Services.Sync;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Tests;

public class OnDemandSyncLimiterTests
{
    private static readonly DateTimeOffset Start = new(2025, 3, 5, 12, 0, 0, TimeSpan.Zero);

    private static OnDemandSyncLimiter Create(int max, FakeTimeProvider time) =>
        new(Options.Create(new SyncOptions { MaxNewCitiesPerHour = max }), time);

    [Fact]
    public void AllowsUpToTheCeiling_ThenRejectsWithTimeUntilTheOldestSlotFrees()
    {
        var time = new FakeTimeProvider(Start);
        var limiter = Create(3, time);

        for (var i = 0; i < 3; i++)
        {
            Assert.True(limiter.TryAcquire(out _));
            time.Now = time.Now.AddMinutes(10);
        }

        Assert.False(limiter.TryAcquire(out var retryAfter));
        // a vaga mais antiga foi usada há 30 min: libera em 30 min
        Assert.Equal(TimeSpan.FromMinutes(30), retryAfter);
    }

    [Fact]
    public void SlotsExpireAfterOneHour()
    {
        var time = new FakeTimeProvider(Start);
        var limiter = Create(1, time);
        Assert.True(limiter.TryAcquire(out _));

        time.Now = Start.AddMinutes(59);
        Assert.False(limiter.TryAcquire(out _));

        time.Now = Start.AddMinutes(60);
        Assert.True(limiter.TryAcquire(out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ZeroOrNegativeCeilingMeansUnlimited(int max)
    {
        var limiter = Create(max, new FakeTimeProvider(Start));

        for (var i = 0; i < 1000; i++) Assert.True(limiter.TryAcquire(out _));
    }

    [Fact]
    public void IsSafeUnderConcurrency()
    {
        var limiter = Create(25, new FakeTimeProvider(Start));
        var granted = 0;

        Parallel.For(0, 500, attempt =>
        {
            if (limiter.TryAcquire(out _)) Interlocked.Increment(ref granted);
        });

        Assert.Equal(25, granted);
    }
}
