using ApiAlertaDengue.Models;
using ApiAlertaDengue.Services.Cities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiAlertaDengue.Tests;

public class CityCatalogServiceTests
{
    private class FakeIbgeClient(params IbgeMunicipality[] municipalities) : IIbgeClient
    {
        public Task<IReadOnlyList<IbgeMunicipality>> GetMunicipalitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IbgeMunicipality>>(municipalities);
    }

    private static IbgeMunicipality Municipality(int id, string name, string uf) => new()
    {
        Id = id,
        Name = name,
        Microregion = new IbgeMicroregion { Mesoregion = new IbgeMesoregion { State = new IbgeState { Code = uf } } },
    };

    [Fact]
    public void IbgeMunicipality_ParsesBothStateLayouts()
    {
        var classic = System.Text.Json.JsonSerializer.Deserialize<IbgeMunicipality>("""
            {"id":3106200,"nome":"Belo Horizonte","microrregiao":{"mesorregiao":{"UF":{"sigla":"MG"}}}}
            """)!;
        var modern = System.Text.Json.JsonSerializer.Deserialize<IbgeMunicipality>("""
            {"id":3106200,"nome":"Belo Horizonte","microrregiao":null,
             "regiao-imediata":{"regiao-intermediaria":{"UF":{"sigla":"MG"}}}}
            """)!;

        Assert.Equal("MG", classic.StateCode);
        Assert.Equal("MG", modern.StateCode);
    }

    [Fact]
    public async Task RefreshAsync_InsertsNewCities_AndKeepsTracking()
    {
        using var testDb = new TestDb();
        await using (var seed = testDb.CreateContext())
        {
            seed.Cities.Add(new City { Geocode = 3106200, Name = "BH antigo", State = "MG", IsTracked = true });
            await seed.SaveChangesAsync();
        }

        await using var db = testDb.CreateContext();
        var service = new CityCatalogService(db, new FakeIbgeClient(
            Municipality(3106200, " Belo Horizonte ", "mg"),
            Municipality(3550308, "São Paulo", "SP"),
            Municipality(3550308, "duplicado", "SP"),
            Municipality(0, "inválido", "SP")),
            NullLogger<CityCatalogService>.Instance);

        var inserted = await service.RefreshAsync();

        Assert.Equal(1, inserted);
        var cities = await db.Cities.AsNoTracking().OrderBy(c => c.Geocode).ToListAsync();
        Assert.Equal(2, cities.Count);
        Assert.Equal("Belo Horizonte", cities[0].Name);
        Assert.Equal("MG", cities[0].State);
        Assert.True(cities[0].IsTracked);
        Assert.Equal("São Paulo", cities[1].Name);
        Assert.False(cities[1].IsTracked);
    }

    [Fact]
    public async Task RefreshAsync_ThrowsWhenIbgeReturnsNothingUsable()
    {
        using var testDb = new TestDb();
        await using var db = testDb.CreateContext();
        var service = new CityCatalogService(db, new FakeIbgeClient(), NullLogger<CityCatalogService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefreshAsync());
    }
}
