using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Data;
using LogisticaBackend.Interfaces;
using LogisticaBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregar servicios de Controladores y OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 2. Configurar PostgreSQL con Entity Framework Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// 3. Configuración CORS para el frontend (React / Vite)
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirReact", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000") // Puertos habituales de React/Vite
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 4. Inyección de dependencias para el servicio OCR de Azure
builder.Services.AddScoped<IOcrAzureService, OcrAzureService>();
builder.Services.AddScoped<TuProyecto.Application.Interfaces.IOcrAzureService, OcrAzureService>();

var app = builder.Build();

// 5. Configurar el pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Habilitar CORS antes del mapeo de rutas
app.UseCors("PermitirReact");

app.UseAuthorization();

app.MapControllers();

app.Run();
