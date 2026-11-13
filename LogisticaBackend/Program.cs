using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
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
builder.Services.AddControllers(o =>
{
    o.Filters.Add<ValidarAntiforgeryFilter>(); // CSRF en todo POST/PUT/PATCH/DELETE
    o.Filters.Add<ValidacionFluentFilter>();
});
builder.Services.AddAntiforgeryLogistica();
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
builder.Services.AddProblemDetailsLogistica();
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
builder.Services.AddOptions<LiquidacionesOptions>()
    .Bind(builder.Configuration.GetSection(LiquidacionesOptions.SectionName))
    .Validate(o => o.TopeValorMonetario > 0 && o.MaximoRenglonesGasto > 0, "Liquidaciones: valores inválidos.")
    .ValidateOnStart();
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

// 9. Límite de peticiones: login por IP y por usuario, OCR por usuario y global (Microsoft.AspNetCore.RateLimiting).
builder.Services.AddLimitesPeticiones(builder.Configuration);

// 9b. IP real detrás de Caddy. Solo se confía en el proxy local (loopback) y en los proxies
// listados en Proxy:ProxiesConocidos (p. ej. la IP del contenedor de Caddy).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    foreach (var ip in builder.Configuration.GetSection("Proxy:ProxiesConocidos").Get<string[]>() ?? Array.Empty<string>())
    {
        o.KnownProxies.Add(System.Net.IPAddress.Parse(ip));
    }
});

// 9c. Límites de tamaño: 1 MB por defecto; solo los endpoints que suben archivos amplían a 10 MB.
const long TamanoMaximoCuerpo = 1024 * 1024;
builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false; // no revelar el servidor
    k.Limits.MaxRequestBodySize = TamanoMaximoCuerpo;
    k.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
    k.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = TamanoMaximoCuerpo;
    o.ValueCountLimit = 50;
    o.ValueLengthLimit = 64 * 1024;
    o.MultipartHeadersLengthLimit = 16 * 1024;
    o.MemoryBufferThreshold = 64 * 1024;
});

// 10. CORS: orígenes desde Cors:OrigenesPermitidos, sin comodines. Con el despliegue en el mismo
// origen (Caddy) la lista puede quedar vacía: el navegador no necesita CORS.
var origenesPermitidos = builder.Configuration.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? Array.Empty<string>();
if (origenesPermitidos.Any(o => o.Contains('*')))
{
    throw new InvalidOperationException("Cors:OrigenesPermitidos no admite comodines.");
}
builder.Services.AddCors(options =>
{
    options.AddPolicy(PoliticaCors, policy =>
    {
        policy.WithOrigins(origenesPermitidos)
              .AllowCredentials()
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .WithHeaders("Content-Type", "Accept", ConfiguracionAntiforgery.NombreEncabezado)
              .WithExposedHeaders("Retry-After", "Content-Disposition")
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// 11. HSTS fuera de Development (Caddy termina TLS; X-Forwarded-Proto evita bucles de redirección).
builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(365);
    o.IncludeSubDomains = true;
    o.Preload = false;
});

var app = builder.Build();

app.UseForwardedHeaders(); // primero: el resto del pipeline ve la IP real
app.UseEncabezadosSeguridad(app.Environment);

// Falla rápido: sin cadena de conexión válida la aplicación no arranca.
ValidadorConfiguracion.ObtenerCadenaConexion(app.Configuration, app.Environment);
await InicializadorBaseDatos.InicializarAsync(app.Services);

// Pipeline de Middleware
app.UseExceptionHandler();
app.UseStatusCodePages(); // 404/405/415 sin cuerpo también responden ProblemDetails en español

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Logística API v1");
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseCors(PoliticaCors);

app.UseAuthentication();
app.UseRateLimiter(); // después de autenticar: los límites por usuario usan la sesión
app.UseAuthorization();

app.MapControllers();

// Endpoints de Health Check
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

await app.RunAsync();

// Expuesto para WebApplicationFactory en las pruebas de integración.
public partial class Program
{
    private const string PoliticaCors = "Frontend";
}
