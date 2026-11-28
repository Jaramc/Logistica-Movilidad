using LogisticaBackend.DTOs;
using LogisticaBackend.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LogisticaBackend.Tests.Integracion.Infraestructura;

/// <summary>
/// La API completa (Program real: Identity, CSRF, rate limiting, encabezados, auditoría…) contra
/// un PostgreSQL real. Solo se reemplaza el OCR de Azure por un doble controlable.
/// </summary>
public class FabricaApi : WebApplicationFactory<Program>
{
    private readonly string _cadenaConexion;
    private readonly Dictionary<string, string?> _ajustes;

    public OcrFalso Ocr { get; } = new();

    public FabricaApi(PostgresFixture postgres, string baseDeDatos, Dictionary<string, string?>? ajustes = null)
    {
        _cadenaConexion = postgres.CadenaConexion(baseDeDatos);
        _ajustes = new Dictionary<string, string?>
        {
            ["BaseDatos:AplicarMigraciones"] = "true",
            // Revalidar la sesión en cada petición: así se prueba que desactivar cierra sesiones.
            ["Seguridad:IntervaloValidacionSesion"] = "00:00:00",
            // Límites altos por defecto; la prueba de rate limiting usa su propia fábrica.
            ["LimitePeticiones:LoginPorIp"] = "10000",
            ["LimitePeticiones:LoginPorUsuario"] = "10000",
            ["LimitePeticiones:OcrPorUsuario"] = "10000",
            ["LimitePeticiones:GlobalPorCliente"] = "100000",
        };
        foreach (var (clave, valor) in ajustes ?? new())
        {
            _ajustes[clave] = valor;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Pruebas"); // ni Development (Swagger) ni Production (exige secretos reales)
        builder.UseSetting("ConnectionStrings:DefaultConnection", _cadenaConexion);
        foreach (var (clave, valor) in _ajustes)
        {
            builder.UseSetting(clave, valor);
        }

        builder.ConfigureTestServices(servicios =>
        {
            servicios.RemoveAll<IPlanillaOcrService>();
            servicios.AddSingleton<IPlanillaOcrService>(Ocr);
        });
    }
}

/// <summary>Doble del OCR: no llama a Azure. Puede forzarse a lanzar una excepción.</summary>
public sealed class OcrFalso : IPlanillaOcrService
{
    public Exception? ExcepcionAForzar { get; set; }

    public Task<PlanillaOcrResponseDto> ProcesarPlanillaAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (ExcepcionAForzar is not null) throw ExcepcionAForzar;
        return Task.FromResult(new PlanillaOcrResponseDto("Procesado",
            new PlanillaOcrDataDto("WCN410", "BOGOTA", "MEDELLIN", 0m, 2_800_000m, 0m, 0m,
                new List<GastoDetalleOcrDto> { new("Peaje Pescadero", 45_000m, 0.9f), new("ACPM", 1_158_300m, 0.8f) },
                ConfianzaPromedio: 0.85f,
                CamposNoEncontrados: new List<string> { "Flete" },
                JsonOcrCrudo: JsonOcrCrudoFalso)));
    }

    public const string JsonOcrCrudoFalso =
        """[{"nombre":"Placa","content":"WCN410","confidence":0.9},{"nombre":"Anticipo","content":"$ 2.800.000","confidence":0.8}]""";
}
