using ApiAlertaDengue.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace ApiAlertaDengue.Tests;

public class AdminApiKeyFilterTests
{
    private static IActionResult? Run(string configuredKey, string? headerValue)
    {
        var http = new DefaultHttpContext();
        if (headerValue is not null) http.Request.Headers[AdminApiKeyFilter.HeaderName] = headerValue;

        var context = new AuthorizationFilterContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()), []);

        new AdminApiKeyFilter(Options.Create(new AdminOptions { ApiKey = configuredKey })).OnAuthorization(context);
        return context.Result;
    }

    [Fact]
    public void WithoutConfiguredKey_EndpointsAreHidden() =>
        Assert.IsType<NotFoundResult>(Run("", "qualquer"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("errada")]
    public void WithMissingOrWrongKey_IsUnauthorized(string? header) =>
        Assert.IsType<UnauthorizedResult>(Run("segredo", header));

    [Fact]
    public void WithCorrectKey_IsAllowed() =>
        Assert.Null(Run("segredo", "segredo"));
}
