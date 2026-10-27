using ApiAlertaDengue.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ApiAlertaDengue.Tests;

/// <summary>Banco SQLite em memória, descartado ao final do teste.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteConnection Connection => _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDb()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
