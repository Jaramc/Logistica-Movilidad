using System.Collections.Concurrent;

namespace LogisticaBackend.Tests.Integracion.Infraestructura;

/// <summary>
/// Una fábrica (y una base de datos migrada) por nombre, reutilizada por las pruebas de la
/// colección. Las pruebas de la colección corren en serie, así que compartirla es seguro: cada
/// prueba crea sus propios usuarios con correos únicos.
/// </summary>
public static class Fabricas
{
    private static readonly ConcurrentDictionary<string, FabricaApi> Cache = new();

    public static FabricaApi Principal(PostgresFixture postgres) =>
        Cache.GetOrAdd("principal", _ => Crear(postgres, "logistica_principal"));

    /// <summary>Límites de peticiones reales (los de producción) para probar el 429.</summary>
    public static FabricaApi ConLimitesReales(PostgresFixture postgres) =>
        Cache.GetOrAdd("limites", _ => Crear(postgres, "logistica_limites", new()
        {
            ["LimitePeticiones:LoginPorIp"] = "5",
            ["LimitePeticiones:LoginPorUsuario"] = "5",
        }));

    private static FabricaApi Crear(PostgresFixture postgres, string baseDeDatos, Dictionary<string, string?>? ajustes = null)
    {
        var fabrica = new FabricaApi(postgres, baseDeDatos, ajustes);
        _ = fabrica.Server; // arranca la app: migra la base y crea los roles
        return fabrica;
    }
}
