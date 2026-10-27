using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Services.Cities;

public class IbgeClient(HttpClient httpClient, IOptions<IbgeOptions> options) : IIbgeClient
{
    public async Task<IReadOnlyList<IbgeMunicipality>> GetMunicipalitiesAsync(CancellationToken cancellationToken = default)
    {
        var municipalities = await httpClient.GetFromJsonAsync<List<IbgeMunicipality>>(
            options.Value.MunicipalitiesUrl, cancellationToken);

        return municipalities ?? [];
    }
}
