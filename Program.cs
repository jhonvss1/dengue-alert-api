using ApiAlertaDengue.Data;
using ApiAlertaDengue.Security;
using ApiAlertaDengue.Services.Alerts;
using ApiAlertaDengue.Services.Cities;
using ApiAlertaDengue.Services.InfoDengue;
using ApiAlertaDengue.Services.Sync;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var serverVersion = new MySqlServerVersion(
    Version.Parse(builder.Configuration["Database:ServerVersion"] ?? "8.4.0"));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion, mysql => mysql.EnableRetryOnFailure()));

builder.Services.AddInfoDengueClient(builder.Configuration);
builder.Services.AddCityCatalog(builder.Configuration);
builder.Services.AddCitySync(builder.Configuration);
builder.Services.AddScoped<ICityAlertsService, CityAlertsService>();

builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.Section));
builder.Services.AddScoped<AdminApiKeyFilter>();

// Origens permitidas (ex.: https://dengue.brago.com.br). Em desenvolvimento, sem configuração, libera qualquer origem.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    else if (builder.Environment.IsDevelopment())
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
}));

// A API roda atrás do túnel da Cloudflare: respeita X-Forwarded-* (o TLS termina na Cloudflare).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API de Alerta de Dengue v1");
        c.RoutePrefix = string.Empty;
    });
    app.UseHttpsRedirection();
}

await DatabaseInitializer.MigrateAsync(app.Services);

app.UseCors("Frontend");
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
