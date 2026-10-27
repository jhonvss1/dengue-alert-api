namespace ApiAlertaDengue.Models;

public class DengueAlert
{
    public int Id { get; set; }
    public int Geocode { get; set; }
    public City? City { get; set; }
    public Disease Disease { get; set; }

    /// <summary>Ano epidemiológico.</summary>
    public int Year { get; set; }

    /// <summary>Semana epidemiológica (1 a 53).</summary>
    public int Week { get; set; }

    /// <summary>Primeiro dia (domingo) da semana epidemiológica.</summary>
    public DateOnly? WeekStart { get; set; }

    public double EstimatedCases { get; set; }
    public int ReportedCases { get; set; }
    public double? IncidencePer100k { get; set; }
    public AlertLevel AlertLevel { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
