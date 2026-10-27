using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiAlertaDengue.Helpers;
using ApiAlertaDengue.Models;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Services.InfoDengue;

public class InfoDengueClient(HttpClient httpClient, IOptions<InfoDengueOptions> options) : IInfoDengueClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<IReadOnlyList<DengueAlert>> GetAlertsAsync(
        int geocode,
        Disease disease,
        EpidemiologicalWeek.Week from,
        EpidemiologicalWeek.Week to,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(options.Value.BaseUrl, geocode, disease, from, to);

        List<InfoDengueRecord>? records;
        try
        {
            records = await httpClient.GetFromJsonAsync<List<InfoDengueRecord>>(url, JsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException
                                       && !cancellationToken.IsCancellationRequested)
        {
            throw new InfoDengueException(
                $"Falha ao consultar o InfoDengue (geocode {geocode}, {disease.ToApiName()}).", ex);
        }

        return Map(records ?? [], geocode, disease);
    }

    internal static string BuildUrl(
        string baseUrl, int geocode, Disease disease, EpidemiologicalWeek.Week from, EpidemiologicalWeek.Week to)
    {
        var query = new Dictionary<string, string>
        {
            ["geocode"] = geocode.ToString(),
            ["disease"] = disease.ToApiName(),
            ["format"] = "json",
            ["ew_start"] = from.Number.ToString(),
            ["ew_end"] = to.Number.ToString(),
            ["ey_start"] = from.Year.ToString(),
            ["ey_end"] = to.Year.ToString(),
        };

        var queryString = string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        return $"{baseUrl}?{queryString}";
    }

    internal static IReadOnlyList<DengueAlert> Map(IEnumerable<InfoDengueRecord> records, int geocode, Disease disease)
    {
        var byWeek = new Dictionary<int, DengueAlert>();

        foreach (var record in records)
        {
            if (!EpidemiologicalWeek.TryParseKey(record.Se, out var week)) continue;

            byWeek[record.Se] = new DengueAlert
            {
                Geocode = geocode,
                Disease = disease,
                Year = week.Year,
                Week = week.Number,
                WeekStart = record.WeekStartUnixMs is { } ms
                    ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime)
                    : EpidemiologicalWeek.StartOf(week),
                EstimatedCases = record.EstimatedCases ?? 0,
                ReportedCases = (int)Math.Round(record.ReportedCases ?? 0),
                IncidencePer100k = record.IncidencePer100k,
                AlertLevel = record.Level is >= 1 and <= 4 ? (AlertLevel)record.Level.Value : AlertLevel.Unknown,
            };
        }

        return byWeek.Values.OrderBy(a => a.Year).ThenBy(a => a.Week).ToList();
    }
}
