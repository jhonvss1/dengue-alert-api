using ApiAlertaDengue.Models;

namespace ApiAlertaDengue.Contracts;

public record CityDto(int Geocode, string Name, string State, bool IsTracked, DateTime? LastSyncedAt)
{
    public static CityDto From(City city) =>
        new(city.Geocode, city.Name, city.State, city.IsTracked, city.LastSyncedAt);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
