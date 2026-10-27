namespace ApiAlertaDengue.Security;

public class AdminOptions
{
    public const string Section = "Admin";

    /// <summary>Chave exigida no header X-Api-Key. Vazia = endpoints administrativos desabilitados.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
