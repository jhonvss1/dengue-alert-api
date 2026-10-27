using System.Text.Json.Serialization;

namespace ApiAlertaDengue.Services.Cities;

public class IbgeMunicipality
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nome")]
    public string? Name { get; set; }

    [JsonPropertyName("microrregiao")]
    public IbgeMicroregion? Microregion { get; set; }

    [JsonPropertyName("regiao-imediata")]
    public IbgeImmediateRegion? ImmediateRegion { get; set; }

    /// <summary>A UF vem da microrregião (formato clássico) ou da região intermediária (formato novo).</summary>
    public string? StateCode =>
        Microregion?.Mesoregion?.State?.Code ?? ImmediateRegion?.IntermediateRegion?.State?.Code;
}

public class IbgeMicroregion
{
    [JsonPropertyName("mesorregiao")]
    public IbgeMesoregion? Mesoregion { get; set; }
}

public class IbgeMesoregion
{
    [JsonPropertyName("UF")]
    public IbgeState? State { get; set; }
}

public class IbgeImmediateRegion
{
    [JsonPropertyName("regiao-intermediaria")]
    public IbgeIntermediateRegion? IntermediateRegion { get; set; }
}

public class IbgeIntermediateRegion
{
    [JsonPropertyName("UF")]
    public IbgeState? State { get; set; }
}

public class IbgeState
{
    [JsonPropertyName("sigla")]
    public string? Code { get; set; }
}
