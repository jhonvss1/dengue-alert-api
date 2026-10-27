namespace ApiAlertaDengue.Services.Cities;

public static class CityServiceCollectionExtensions
{
    public static IServiceCollection AddCityCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IbgeOptions>(configuration.GetSection(IbgeOptions.Section));

        services.AddHttpClient<IIbgeClient, IbgeClient>(c => c.Timeout = TimeSpan.FromSeconds(60))
            .AddStandardResilienceHandler(o =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(180);
            });

        services.AddScoped<ICityCatalogService, CityCatalogService>();
        services.AddHostedService<CityCatalogSeeder>();
        return services;
    }
}
