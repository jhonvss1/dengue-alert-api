using ApiAlertaDengue.Contracts;
using ApiAlertaDengue.Controllers;
using ApiAlertaDengue.Models;
using ApiAlertaDengue.Services.Alerts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiAlertaDengue.Tests;

public class CitiesControllerTests
{
    private class FakeAlertsService(AlertsOutcome outcome) : ICityAlertsService
    {
        public Task<PagedResult<CityDto>> SearchCitiesAsync(string? search, string? state, int page, int pageSize, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<CityDto?> GetCityAsync(int geocode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AlertsOutcome> GetAlertsAsync(int geocode, Disease? disease, int weeks, CancellationToken ct = default) =>
            Task.FromResult(outcome);
        public Task<AlertsOutcome> GetLatestAsync(int geocode, CancellationToken ct = default) => Task.FromResult(outcome);
    }

    private static CitiesController Create(AlertsOutcome outcome) => new(new FakeAlertsService(outcome))
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };

    [Fact]
    public async Task RateLimited_Returns429_WithRetryAfterHeader()
    {
        var controller = Create(new AlertsOutcome(AlertsStatus.RateLimited, RetryAfter: TimeSpan.FromSeconds(125.2)));

        var result = await controller.GetAlerts(1, null, 12);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(429, objectResult.StatusCode);
        Assert.Equal(429, Assert.IsType<ProblemDetails>(objectResult.Value).Status);
        Assert.Equal("126", controller.Response.Headers.RetryAfter.ToString());
    }

    [Theory]
    [InlineData(AlertsStatus.CityNotFound, 404)]
    [InlineData(AlertsStatus.SourceUnavailable, 503)]
    public async Task OtherFailures_KeepTheirStatusCodes(AlertsStatus status, int expected)
    {
        var result = await Create(new AlertsOutcome(status)).GetLatest(1, CancellationToken.None);

        Assert.Equal(expected, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
    }
}
