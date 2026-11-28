using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.Services;
using LogisticaBackend.Tests.Integracion.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LogisticaBackend.Tests.Integracion;

/// <summary>Bloque 2 de la fase 1: concurrencia e inmutabilidad de movimientos_saldo.</summary>
[Collection(ColeccionIntegracion.Nombre)]
public class CuentaConductorIntegracionTests
{
    private readonly FabricaApi _fabrica;

    public CuentaConductorIntegracionTests(PostgresFixture postgres) => _fabrica = Fabricas.Principal(postgres);

    private static string CedulaPrueba() => Random.Shared.Next(100_000_000, 999_999_999).ToString();
    private static string CodigoPrueba() => "C" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant();

    private async Task<int> CrearConductorAsync()
    {
        var cedula = CedulaPrueba();
        return await Sembrador.EnScopeAsync(_fabrica, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.Conductores.Add(new Conductor { Codigo = CodigoPrueba(), NombreCompleto = "CONDUCTOR PRUEBA CUENTA", Cedula = cedula });
            await db.SaveChangesAsync();
            return await db.Conductores.AsNoTracking().Where(c => c.Cedula == cedula).Select(c => c.Id).SingleAsync();
        });
    }

    [Fact]
    public async Task Pagos_20Concurrentes_TerminanConElSaldoExactoY20Movimientos()
    {
        var conductorId = await CrearConductorAsync();

        var tareas = Enumerable.Range(0, 20).Select(_ => Sembrador.EnScopeAsync(_fabrica, async sp =>
        {
            var cuenta = sp.GetRequiredService<ICuentaConductorService>();
            await cuenta.RegistrarPagoAsync(conductorId, null, TipoPago.DescuentoNomina, 1_000m, DateOnly.FromDateTime(DateTime.UtcNow),
                null, null, null, null);
            return true;
        }));
        await Task.WhenAll(tareas);

        var (saldoFinal, totalMovimientos) = await Sembrador.EnScopeAsync(_fabrica, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var cuenta = sp.GetRequiredService<ICuentaConductorService>();
            var saldo = await cuenta.ObtenerSaldoAsync(conductorId);
            var total = await db.MovimientosSaldo.CountAsync(m => m.ConductorId == conductorId);
            return (saldo, total);
        });

        saldoFinal.ShouldBe(20_000m);
        totalMovimientos.ShouldBe(20);
    }

    [Fact]
    public async Task MovimientosSaldo_UpdateDirecto_Rechazado()
    {
        var conductorId = await CrearConductorAsync();
        await Sembrador.EnScopeAsync(_fabrica, async sp =>
            await sp.GetRequiredService<ICuentaConductorService>()
                .RegistrarAjusteAsync(conductorId, 100m, "Ajuste de prueba", null));

        var sqlUpdate = $"UPDATE movimientos_saldo SET valor = 999 WHERE conductor_id = {conductorId}";
        var error = await Should.ThrowAsync<Npgsql.PostgresException>(() => Sembrador.EnScopeAsync(_fabrica, sp =>
            sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(sqlUpdate)));

        error.Message.ShouldContain("solo inserción");

        var sqlDelete = $"DELETE FROM movimientos_saldo WHERE conductor_id = {conductorId}";
        await Should.ThrowAsync<Npgsql.PostgresException>(() => Sembrador.EnScopeAsync(_fabrica, sp =>
            sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(sqlDelete)));
    }
}
