namespace ApiAlertaDengue.Services.Cities;

public class IbgeOptions
{
    public const string Section = "Ibge";

    public string MunicipalitiesUrl { get; set; } =
        "https://servicodados.ibge.gov.br/api/v1/localidades/municipios";
}
