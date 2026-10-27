using ApiAlertaDengue.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var serverVersion = new MySqlServerVersion(
    Version.Parse(builder.Configuration["Database:ServerVersion"] ?? "8.4.0"));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion, mysql => mysql.EnableRetryOnFailure()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API de Alerta de Dengue v1");
        c.RoutePrefix = string.Empty;
    });
}

await DatabaseInitializer.MigrateAsync(app.Services);

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
