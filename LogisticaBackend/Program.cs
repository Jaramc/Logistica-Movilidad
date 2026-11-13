using System.Threading.RateLimiting;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LogisticaBackend.Auth;
using LogisticaBackend.Configuration;
using LogisticaBackend.Data;
using LogisticaBackend.Interfaces;
using LogisticaBackend.Middleware;
using LogisticaBackend.Services;
using LogisticaBackend.Validators;

var builder = WebApplication.CreateBuilder(args);

// 1. Controladores y Swagger
builder.Services.AddControllers(o => o.Filters.Add<ValidacionFluentFilter>());
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
// Se lee de user-secrets (desarrollo) o de variables de entorno AzureDocumentIntelligence__*.
builder.Services.AddOptions<AzureOcrOptions>()
    .Bind(builder.Configuration.GetSection(AzureOcrOptions.SectionName));

// 5. Inyección Singleton de DocumentAnalysisClient (Reutilización de Sockets HTTP)
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<AzureOcrOptions>>().Value;
    var entorno = sp.GetRequiredService<IHostEnvironment>();
    ValidadorConfiguracion.ValidarAzure(options, entorno);

    if (string.IsNullOrWhiteSpace(options.Endpoint) || string.IsNullOrWhiteSpace(options.ApiKey))
    {
        // Solo fuera de Production: permite arrancar sin Azure; el OCR responderá 502.
        sp.GetRequiredService<ILogger<Program>>().LogWarning(
            "Azure Document Intelligence no está configurado. El OCR no funcionará en este entorno.");
        return new DocumentAnalysisClient(new Uri("https://no-configurado.invalid/"), new AzureKeyCredential("no-configurado"));
    }
    return new DocumentAnalysisClient(new Uri(options.Endpoint), new AzureKeyCredential(options.ApiKey));
});

// 6. Registro de Servicios de Dominio e Infraestructura
builder.Services.AddScoped<IPlanillaOcrService, PlanillaOcrService>();
builder.Services.AddScoped<IExcelExportService, ExcelExportService>();

// 7. Base de Datos PostgreSQL con Entity Framework Core (obligatoria).
// En Production se niega a arrancar si falta la cadena o contiene valores de ejemplo.
builder.Services.AddDbContext<AppDbContext>((sp, options) => options.UseNpgsql(
    ValidadorConfiguracion.ObtenerCadenaConexion(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<IHostEnvironment>())));

// 7b. Identidad: ASP.NET Core Identity + cookie de sesión + 2FA obligatorio + políticas.
builder.Services.AddIdentidadLogistica(builder.Configuration);

// 8. Health Checks para Monitoreo de Liveness y Readiness
builder.Services.AddHealthChecks()
    .AddCheck<BaseDatosHealthCheck>("database", tags: new[] { "ready" });

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

// Falla rápido: sin cadena de conexión válida la aplicación no arranca.
ValidadorConfiguracion.ObtenerCadenaConexion(app.Configuration, app.Environment);
await InicializadorBaseDatos.InicializarAsync(app.Services);

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Endpoints de Health Check
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

await app.RunAsync();

// Expuesto para WebApplicationFactory en las pruebas de integración.
public partial class Program;
