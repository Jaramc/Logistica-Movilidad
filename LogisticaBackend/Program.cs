using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Data;
using LogisticaBackend.Interfaces;
using LogisticaBackend.Services;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregar servicios de Controladores y OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 2. Configurar PostgreSQL con Entity Framework Core (Falla rápida si la conexión no está configurada)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada en appsettings.json o variables de entorno.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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

// 4. Inyección de dependencias para el cliente y servicio OCR de Azure
// Registro Singleton de DocumentAnalysisClient para reutilizar conexiones HTTP y evitar agotamiento de sockets
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["Azure:FormRecognizer:Endpoint"];
    var apiKey = config["Azure:FormRecognizer:ApiKey"];

    if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
    {
        throw new InvalidOperationException("Las credenciales de Azure Form Recognizer ('Azure:FormRecognizer:Endpoint' y 'Azure:FormRecognizer:ApiKey') no están configuradas.");
    }

    return new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
});

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
