namespace ApiAlertaDengue.Services.Cities;

public interface IIbgeClient
{
    Task<IReadOnlyList<IbgeMunicipality>> GetMunicipalitiesAsync(CancellationToken cancellationToken = default);
}
