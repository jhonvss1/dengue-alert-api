using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Security;

public class AdminApiKeyFilter(IOptions<AdminOptions> options) : IAuthorizationFilter
{
    public const string HeaderName = "X-Api-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = options.Value.ApiKey;

        // Sem chave configurada, o recurso fica como se não existisse.
        if (string.IsNullOrWhiteSpace(expected))
        {
            context.Result = new NotFoundResult();
            return;
        }

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        var valid = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

        if (!valid) context.Result = new UnauthorizedResult();
    }
}
