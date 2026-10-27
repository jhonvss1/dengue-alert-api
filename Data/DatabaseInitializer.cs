using Microsoft.EntityFrameworkCore;

namespace ApiAlertaDengue.Data;

public static class DatabaseInitializer
{
    /// <summary>Aplica as migrations, aguardando o banco ficar disponível (útil ao subir via docker compose).</summary>
    public static async Task MigrateAsync(IServiceProvider services, int maxAttempts = 10)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Banco indisponível (tentativa {Attempt}/{Max}). Nova tentativa em 3s.", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
