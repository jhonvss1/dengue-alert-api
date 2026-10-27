using ApiAlertaDengue.Data;
using ApiAlertaDengue.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiAlertaDengue.Services.Cities;

public class CityCatalogService(
    AppDbContext db,
    IIbgeClient ibge,
    ILogger<CityCatalogService> logger) : ICityCatalogService
{
    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var municipalities = await ibge.GetMunicipalitiesAsync(cancellationToken);
        var valid = municipalities
            .Where(m => m.Id > 0 && !string.IsNullOrWhiteSpace(m.Name) && m.StateCode is { Length: 2 })
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .ToList();

        if (valid.Count == 0)
        {
            throw new InvalidOperationException("O IBGE não devolveu nenhum município válido.");
        }

        var existing = await db.Cities.ToDictionaryAsync(c => c.Geocode, cancellationToken);
        var inserted = 0;

        foreach (var m in valid)
        {
            var name = m.Name!.Trim();
            var state = m.StateCode!.ToUpperInvariant();

            if (existing.TryGetValue(m.Id, out var city))
            {
                city.Name = name;
                city.State = state;
            }
            else
            {
                db.Cities.Add(new City { Geocode = m.Id, Name = name, State = state });
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Catálogo de municípios atualizado: {Total} municípios, {Inserted} novos.",
            valid.Count, inserted);
        return inserted;
    }
}
