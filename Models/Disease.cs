namespace ApiAlertaDengue.Models;

public enum Disease
{
    Dengue = 1,
    Chikungunya = 2,
    Zika = 3,
}

public static class DiseaseExtensions
{
    /// <summary>Valor esperado pelo parâmetro "disease" da API do InfoDengue.</summary>
    public static string ToApiName(this Disease disease) => disease switch
    {
        Disease.Dengue => "dengue",
        Disease.Chikungunya => "chikungunya",
        Disease.Zika => "zika",
        _ => throw new ArgumentOutOfRangeException(nameof(disease), disease, null),
    };
}
