using System.Net;
using ApiAlertaDengue.Helpers;
using ApiAlertaDengue.Models;
using ApiAlertaDengue.Services.InfoDengue;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Tests;

public class InfoDengueClientTests
{
    private static readonly EpidemiologicalWeek.Week From = new(2024, 50);
    private static readonly EpidemiologicalWeek.Week To = new(2025, 10);

    private static InfoDengueClient CreateClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(new InfoDengueOptions { BaseUrl = "https://example.test/api/alertcity" }));

    [Fact]
    public async Task GetAlertsAsync_BuildsQueryAndMapsRecords()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json("""
            [
              {"SE":202510,"data_iniSE":1740873600000,"casos_est":992.0,"casos":470,"p_inc100k":38.2,"nivel":2},
              {"SE":202509,"data_iniSE":1740268800000,"casos_est":850.5,"casos":410,"p_inc100k":null,"nivel":3}
            ]
            """));

        var alerts = await CreateClient(handler).GetAlertsAsync(3106200, Disease.Chikungunya, From, To);

        var uri = Assert.Single(handler.Requests);
        Assert.Equal("geocode=3106200&disease=chikungunya&format=json&ew_start=50&ew_end=10&ey_start=2024&ey_end=2025",
            uri.Query.TrimStart('?'));

        Assert.Equal(2, alerts.Count);
        Assert.Equal(202509, alerts[0].Year * 100 + alerts[0].Week); // ordenado por semana
        Assert.Equal(AlertLevel.Orange, alerts[0].AlertLevel);
        Assert.Null(alerts[0].IncidencePer100k);

        var latest = alerts[1];
        Assert.Equal(3106200, latest.Geocode);
        Assert.Equal(Disease.Chikungunya, latest.Disease);
        Assert.Equal(992.0, latest.EstimatedCases);
        Assert.Equal(470, latest.ReportedCases);
        Assert.Equal(new DateOnly(2025, 3, 2), latest.WeekStart);
        Assert.Equal(AlertLevel.Yellow, latest.AlertLevel);
    }

    [Fact]
    public async Task GetAlertsAsync_IgnoresInvalidWeeksAndUnknownLevels()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json("""
            [
              {"SE":0,"casos_est":1,"casos":1,"nivel":1},
              {"SE":202510,"casos_est":null,"casos":null,"nivel":null}
            ]
            """));

        var alerts = await CreateClient(handler).GetAlertsAsync(1, Disease.Zika, From, To);

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertLevel.Unknown, alert.AlertLevel);
        Assert.Equal(0, alert.EstimatedCases);
        Assert.Equal(new DateOnly(2025, 3, 2), alert.WeekStart); // calculado quando a fonte não informa
    }

    [Fact]
    public async Task GetAlertsAsync_ReturnsEmptyForEmptyResponse()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json("[]"));

        Assert.Empty(await CreateClient(handler).GetAlertsAsync(1, Disease.Dengue, From, To));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "[]")]
    [InlineData(HttpStatusCode.OK, "isto não é json")]
    public async Task GetAlertsAsync_WrapsFailuresInInfoDengueException(HttpStatusCode status, string body)
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(body, status));

        await Assert.ThrowsAsync<InfoDengueException>(
            () => CreateClient(handler).GetAlertsAsync(1, Disease.Dengue, From, To));
    }
}
