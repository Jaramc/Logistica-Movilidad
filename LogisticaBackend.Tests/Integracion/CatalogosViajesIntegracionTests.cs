using LogisticaBackend.Data;
using LogisticaBackend.Tests.Integracion.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LogisticaBackend.Tests.Integracion;

/// <summary>
/// Bloque 1 de la fase 1: la migración CatalogosViajesYSaldos aplica limpia sobre un PostgreSQL
/// vacío (lo prueba toda la colección, al migrar la base al arrancar la fábrica) y sus restricciones
/// rechazan datos inválidos aunque alguien salte la API (todavía no existe en este bloque).
/// </summary>
[Collection(ColeccionIntegracion.Nombre)]
public class CatalogosViajesIntegracionTests
{
    private readonly FabricaApi _fabrica;

    public CatalogosViajesIntegracionTests(PostgresFixture postgres) => _fabrica = Fabricas.Principal(postgres);

    private static string CedulaPrueba() => Random.Shared.Next(100_000_000, 999_999_999).ToString();
    private static string CodigoPrueba() => "C" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant();

    [Fact]
    public async Task Migracion_AplicadaLimpia_SiembraLas4Placas()
    {
        // La fábrica ya migró la base al arrancar (AplicarMigraciones=true); si la migración
        // CatalogosViajesYSaldos no aplicara limpio, ninguna prueba de la colección arrancaría.
        // La base es compartida por toda la colección: solo se verifica que las 4 están presentes,
        // no que sean las únicas (otras pruebas siembran sus propios vehículos de prueba).
        var placas = await Sembrador.EnScopeAsync(_fabrica, sp => sp.GetRequiredService<AppDbContext>()
            .Vehiculos.AsNoTracking().Select(v => v.Placa).ToListAsync());

        placas.ShouldContain("JYP052");
        placas.ShouldContain("JYP108");
        placas.ShouldContain("NNN977");
        placas.ShouldContain("STW514");
    }

    [Fact]
    public async Task Conductor_CedulaRepetida_Rechazada()
    {
        var cedula = CedulaPrueba();
        var sqlPrimero = $"INSERT INTO conductores (codigo, nombre_completo, cedula) VALUES ('{CodigoPrueba()}', 'CONDUCTOR PRUEBA UNO', '{cedula}')";
        await Sembrador.EnScopeAsync(_fabrica, sp => sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(sqlPrimero));

        var sqlSegundo = $"INSERT INTO conductores (codigo, nombre_completo, cedula) VALUES ('{CodigoPrueba()}', 'CONDUCTOR PRUEBA DOS', '{cedula}')";
        var error = await Should.ThrowAsync<Npgsql.PostgresException>(() => Sembrador.EnScopeAsync(_fabrica, sp =>
            sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(sqlSegundo)));

        error.SqlState.ShouldBe(Npgsql.PostgresErrorCodes.UniqueViolation);
        error.ConstraintName.ShouldBe("ux_conductores_cedula");
    }

    [Fact]
    public async Task Vehiculo_PlacaInvalida_Rechazada()
    {
        var sql = "INSERT INTO vehiculos (placa, tipo) VALUES ('abc123', 'Tractomula')";

        var error = await Should.ThrowAsync<Npgsql.PostgresException>(() => Sembrador.EnScopeAsync(_fabrica, sp =>
            sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(sql)));

        error.SqlState.ShouldBe(Npgsql.PostgresErrorCodes.CheckViolation);
        error.ConstraintName.ShouldBe("ck_vehiculos_placa_formato");
    }

    [Fact]
    public async Task Viaje_SegundoAbiertoDelMismoConductor_Rechazado()
    {
        var cedula = CedulaPrueba();
        // ZZZ/YYY: nunca chocan con las 4 placas reales sembradas (JYP108, JYP052, NNN977, STW514).
        var placaUno = "ZZZ" + Random.Shared.Next(100, 999);
        var placaDos = "YYY" + Random.Shared.Next(100, 999);

        var error = await Sembrador.EnScopeAsync(_fabrica, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();

            var sqlConductor = $"INSERT INTO conductores (codigo, nombre_completo, cedula) VALUES ('{CodigoPrueba()}', 'CONDUCTOR PRUEBA TRES', '{cedula}')";
            await db.Database.ExecuteSqlRawAsync(sqlConductor);
            var conductorId = await db.Database.SqlQuery<int>($"SELECT id AS \"Value\" FROM conductores WHERE cedula = {cedula}").SingleAsync();

            var sqlVehiculoUno = $"INSERT INTO vehiculos (placa, tipo) VALUES ('{placaUno}', 'Tractomula')";
            await db.Database.ExecuteSqlRawAsync(sqlVehiculoUno);
            var vehiculoUnoId = await db.Database.SqlQuery<int>($"SELECT id AS \"Value\" FROM vehiculos WHERE placa = {placaUno}").SingleAsync();
            var sqlVehiculoDos = $"INSERT INTO vehiculos (placa, tipo) VALUES ('{placaDos}', 'Tractomula')";
            await db.Database.ExecuteSqlRawAsync(sqlVehiculoDos);
            var vehiculoDosId = await db.Database.SqlQuery<int>($"SELECT id AS \"Value\" FROM vehiculos WHERE placa = {placaDos}").SingleAsync();

            var sqlPrimerViaje =
                $"INSERT INTO viajes (conductor_id, vehiculo_id, origen, destino, cliente, fecha_inicio, estado) " +
                $"VALUES ({conductorId}, {vehiculoUnoId}, 'BOGOTA', 'MEDELLIN', 'CLIENTE PRUEBA', '2026-06-01', 'Abierto')";
            await db.Database.ExecuteSqlRawAsync(sqlPrimerViaje);

            var sqlSegundoViaje =
                $"INSERT INTO viajes (conductor_id, vehiculo_id, origen, destino, cliente, fecha_inicio, estado) " +
                $"VALUES ({conductorId}, {vehiculoDosId}, 'BOGOTA', 'CALI', 'CLIENTE PRUEBA', '2026-06-02', 'Abierto')";
            return await Should.ThrowAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(sqlSegundoViaje));
        });

        error.SqlState.ShouldBe(Npgsql.PostgresErrorCodes.UniqueViolation);
        error.ConstraintName.ShouldBe("ux_viajes_conductor_abierto");
    }
}
