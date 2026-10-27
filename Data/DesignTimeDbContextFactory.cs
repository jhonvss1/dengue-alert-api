using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ApiAlertaDengue.Data;

/// <summary>Usada apenas pelo "dotnet ef": não exige banco acessível nem segredos.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql("Server=localhost;Database=design;User=design;Password=design;",
                new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;
        return new AppDbContext(options);
    }
}
