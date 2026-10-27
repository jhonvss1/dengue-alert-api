using ApiAlertaDengue.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApiAlertaDengue.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<DengueAlert> DengueAlerts => Set<DengueAlert>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    /// <summary>O MySQL devolve DateTime sem "Kind"; marcamos como UTC para a API serializar com o sufixo "Z".</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<City>(e =>
        {
            e.Property(c => c.Geocode).ValueGeneratedNever();
            e.Property(c => c.Name).IsRequired();
            e.Property(c => c.State).IsRequired().IsFixedLength();
            e.HasIndex(c => c.Name);
            e.HasIndex(c => new { c.State, c.Name });
            e.HasIndex(c => c.IsTracked);
        });

        modelBuilder.Entity<DengueAlert>(e =>
        {
            e.Property(a => a.Disease).HasConversion<string>().HasMaxLength(20);
            e.HasOne(a => a.City)
                .WithMany(c => c.Alerts)
                .HasForeignKey(a => a.Geocode)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.Geocode, a.Disease, a.Year, a.Week }).IsUnique();
        });

        modelBuilder.Entity<SyncRun>(e =>
        {
            e.Property(r => r.Trigger).HasMaxLength(20);
            e.Property(r => r.Error).HasMaxLength(2000);
            e.HasIndex(r => r.StartedAt);
        });
    }
}

public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

public class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue && v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
