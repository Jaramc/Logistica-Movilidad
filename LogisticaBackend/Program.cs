using System.Threading.RateLimiting;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LogisticaBackend.Configuration;
using LogisticaBackend.Data;
using LogisticaBackend.Interfaces;
using LogisticaBackend.Middleware;
using LogisticaBackend.Services;
using LogisticaBackend.Validators;

var builder = WebApplication.CreateBuilder(args);

// 1. Controladores y Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Logística & Movilidad API — Digitalización de Planillas de Gastos",
        Version = "v1",
        Description = "API RESTful de alto rendimiento para extracción OCR (Azure Document Intelligence), liquidación contable y consolidación en Excel."
    });
});

// 2. Manejo Global de Excepciones y ProblemDetails (RFC 7807)
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// 3. Validación de DTOs con FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<GuardarPlanillaDtoValidator>();

// 4. Configuración Fuertemente Tipada de Azure Document Intelligence
builder.Services.Configure<AzureOcrOptions>(options =>
{
    var config = builder.Configuration;
    options.Endpoint = config["AzureDocumentIntelligence:Endpoint"]
        ?? config["Azure:FormRecognizer:Endpoint"]
        ?? string.Empty;
    options.ApiKey = config["AzureDocumentIntelligence:ApiKey"]
        ?? config["Azure:FormRecognizer:ApiKey"]
        ?? string.Empty;
    options.ModelId = config["AzureDocumentIntelligence:ModelId"]
        ?? "planillas-gastos-v1";
});

// 5. Inyección Singleton de DocumentAnalysisClient (Reutilización de Sockets HTTP)
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<AzureOcrOptions>>().Value;
    if (string.IsNullOrWhiteSpace(options.Endpoint) || string.IsNullOrWhiteSpace(options.ApiKey))
    {
        return new DocumentAnalysisClient(new Uri("https://dummy.cognitiveservices.azure.com/"), new AzureKeyCredential("dummy-key"));
    }
    return new DocumentAnalysisClient(new Uri(options.Endpoint), new AzureKeyCredential(options.ApiKey));
});

// 6. Registro de Servicios de Dominio e Infraestructura
builder.Services.AddScoped<IPlanillaOcrService, PlanillaOcrService>();
builder.Services.AddScoped<IExcelExportService, ExcelExportService>();

// 7. Base de Datos PostgreSQL con Entity Framework Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString) && !connectionString.Contains("tu_password"))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    // Registra AppDbContext con dummy para que la inyección de dependencias no falle si PostgreSQL no está conectado
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql("Host=127.0.0.1;Port=5432;Database=logistica_db;Username=postgres;Password=dummy"));
}

// 8. Health Checks para Monitoreo de Liveness y Readiness
builder.Services.AddHealthChecks()
    .AddAsyncCheck("database", async () =>
    {
        return await Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
    }, tags: new[] { "ready" });

// 9. Rate Limiting por IP para proteger endpoints de IA
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ocr_policy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon_client",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromSeconds(60),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Demasiadas Solicitudes de Extracción",
            Detail = "Ha excedido el límite de solicitudes de análisis OCR permitidas por minuto. Por favor espere 60 segundos.",
            Instance = context.HttpContext.Request.Path
        }, token);
    };
});

// 10. CORS Restringido a Orígenes de Frontend Autorizados
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirReact", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Pipeline de Middleware
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Logística API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("PermitirReact");
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

// Endpoints de Health Check
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();
