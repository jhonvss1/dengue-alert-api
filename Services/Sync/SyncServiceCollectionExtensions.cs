namespace ApiAlertaDengue.Services.Sync;

public static class SyncServiceCollectionExtensions
{
    public static IServiceCollection AddCitySync(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SyncOptions>(configuration.GetSection(SyncOptions.Section));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<CitySyncLocks>();
        services.AddScoped<ICitySyncService, CitySyncService>();
        return services;
    }
}
