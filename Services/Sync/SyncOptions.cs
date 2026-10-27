namespace ApiAlertaDengue.Services.Sync;

public class SyncOptions
{
    public const string Section = "Sync";

    /// <summary>Liga/desliga o agendamento semanal (a sincronização sob demanda continua funcionando).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Dia da semana da rodada semanal.</summary>
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;

    /// <summary>Horário (HH:mm) da rodada semanal, no fuso configurado.</summary>
    public string TimeOfDay { get; set; } = "03:00";

    public string TimeZone { get; set; } = "America/Sao_Paulo";

    /// <summary>Semanas de histórico carregadas na primeira consulta de uma cidade.</summary>
    public int InitialHistoryWeeks { get; set; } = 52;

    /// <summary>Janela mínima reavaliada a cada atualização (a fonte revisa semanas recentes).</summary>
    public int RefreshWindowWeeks { get; set; } = 8;

    /// <summary>Dados mais velhos que isso são atualizados na leitura (com fallback para o dado salvo).</summary>
    public int StaleAfterDays { get; set; } = 14;

    /// <summary>Cidades sincronizadas em paralelo pelo job semanal.</summary>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>Pausa entre cidades de cada worker do job, para não sobrecarregar a fonte.</summary>
    public int DelayBetweenCitiesMs { get; set; } = 500;
}
