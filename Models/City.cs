using System.ComponentModel.DataAnnotations;

namespace ApiAlertaDengue.Models;

public class City
{
    /// <summary>Código IBGE do município (7 dígitos).</summary>
    [Key]
    public int Geocode { get; set; }

    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Sigla da UF.</summary>
    [MaxLength(2)]
    public string State { get; set; } = string.Empty;

    /// <summary>Cidade já consultada: entra na atualização semanal.</summary>
    public bool IsTracked { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    public List<DengueAlert> Alerts { get; set; } = [];
}
