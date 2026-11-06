using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Data;
using LogisticaBackend.Interfaces;
using LogisticaBackend.Services;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregar servicios de Controladores, OpenAPI y Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Configurar PostgreSQL con Entity Framework Core (Opcional si aún no está conectada la BD)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString) && !connectionString.Contains("tu_password"))
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

// 4. Inyección de dependencias para el cliente y servicios OCR de Azure
// Registro Singleton de DocumentAnalysisClient para reutilizar conexiones HTTP y evitar agotamiento de sockets
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["Azure:FormRecognizer:Endpoint"]
        ?? config["AzureDocumentIntelligence:Endpoint"];
    var apiKey = config["Azure:FormRecognizer:ApiKey"]
        ?? config["AzureDocumentIntelligence:ApiKey"];

    if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
    {
        throw new InvalidOperationException("Las credenciales de Azure ('Endpoint' y 'ApiKey') no están configuradas.");
    }

    return new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
});

// Registro de servicios de liquidación y planilla
builder.Services.AddScoped<IOcrAzureService, OcrAzureService>();
builder.Services.AddScoped<TuProyecto.Application.Interfaces.IOcrAzureService, OcrAzureService>();
builder.Services.AddScoped<IPlanillaOcrService, PlanillaOcrService>();

var app = builder.Build();

// 5. Configurar el pipeline HTTP y Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Habilitar CORS antes de autorización y mapeo de rutas
app.UseCors("PermitirReact");

app.UseAuthorization();

app.MapControllers();

app.Run();
