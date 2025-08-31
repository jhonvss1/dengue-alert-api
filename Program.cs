using ApiAlertaDengue.Data;
using ApiAlertaDengue.Interfaces;
using ApiAlertaDengue.Repository;
using ApiAlertaDengue.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader(); 
    });
});
// Configuração do Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registra os serviços e repositórios
builder.Services.AddScoped<IDengueService, DengueService>();
builder.Services.AddScoped<IDengueRepository, DengueAlertRepository>();
builder.Services.AddHttpClient<DengueService>();


builder.Services.AddControllers();

// Adiciona o suporte ao OpenAPI (Swagger)
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment())
{
    // Configura o Swagger
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API de Alerta de Dengue v1");
        c.RoutePrefix = string.Empty;  // Isso configura o Swagger UI para aparecer na raiz do aplicativo (opcional)
    });


    app.MapOpenApi();
}

// Aplica migração automática no banco de dados
using (var serviceScope = app.Services.CreateScope())
{
    var db = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapControllers();
app.UseCors("AllowAll");

// Redirecionamento HTTP para HTTPS (se configurado)
app.UseHttpsRedirection();

app.Run();
//script docker PS C:\Users\jhons> docker run -d --name dengue-alert -e MYSQL_ROOT_PASSWORD=246810
//-e MYSQL_DATABASE=dengue_alert -e MYSQL_USER=admin -e MYSQL_PASSWORD=apialertadengue2025 -p 3306:3306 mysql:8.4