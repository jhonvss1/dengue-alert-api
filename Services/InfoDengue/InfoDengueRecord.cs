using System.Text.Json.Serialization;

namespace ApiAlertaDengue.Services.InfoDengue;

/// <summary>Um registro semanal devolvido pelo endpoint alertcity do InfoDengue.</summary>
public class InfoDengueRecord
{
    /// <summary>Semana epidemiológica no formato AAAASS (ex.: 202510).</summary>
    [JsonPropertyName("SE")]
    public int Se { get; set; }

    /// <summary>Início da semana, em milissegundos desde a época Unix.</summary>
    [JsonPropertyName("data_iniSE")]
    public long? WeekStartUnixMs { get; set; }

    [JsonPropertyName("casos_est")]
    public double? EstimatedCases { get; set; }

    [JsonPropertyName("casos")]
    public double? ReportedCases { get; set; }

    [JsonPropertyName("p_inc100k")]
    public double? IncidencePer100k { get; set; }

    [JsonPropertyName("nivel")]
    public int? Level { get; set; }
}
