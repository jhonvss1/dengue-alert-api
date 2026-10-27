namespace ApiAlertaDengue.Services.InfoDengue;

public static class InfoDengueServiceCollectionExtensions
{
    public static IServiceCollection AddInfoDengueClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InfoDengueOptions>(configuration.GetSection(InfoDengueOptions.Section));

        services.AddHttpClient<IInfoDengueClient, InfoDengueClient>()
            .AddStandardResilienceHandler(o =>
            {
                // Retry com backoff para 5xx/429/timeouts; o circuito abre se a fonte ficar fora do ar.
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(20);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
                o.Retry.MaxRetryAttempts = 3;
            });

        return services;
    }
}
