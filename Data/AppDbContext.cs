using ApiAlertaDengue.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiAlertaDengue.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<DengueAlert> DengueAlerts => Set<DengueAlert>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

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
