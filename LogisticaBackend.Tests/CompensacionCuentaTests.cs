using LogisticaBackend.Domain.Entities;
using LogisticaBackend.Domain.Services;
using Shouldly;
using Xunit;

namespace LogisticaBackend.Tests;

public class CompensacionCuentaTests
{
    private static MovimientoSaldo M(long id, TipoMovimientoSaldo tipo, decimal valor, int? viajeId = null, DateTime? creadoEn = null) => new()
    {
        Id = id,
        ConductorId = 1,
        Tipo = tipo,
        Valor = valor,
        SaldoResultante = 0m, // irrelevante para la compensación
        FechaCorte = DateOnly.FromDateTime(creadoEn ?? DateTime.UtcNow),
        ViajeId = viajeId,
        CreadoEn = creadoEn ?? DateTime.UtcNow,
    };

    [Fact]
    public void CasoDeJunio_InicialSeCancelaYElViajeQuedaCon5700_YElPagoLoSalda()
    {
        var movimientos = new[]
        {
            M(1, TipoMovimientoSaldo.Inicial, -63_500m),
            M(2, TipoMovimientoSaldo.CierreViaje, 69_200m, viajeId: 10),
        };
        var partidas = CompensacionCuenta.Calcular(movimientos);

        partidas.Single(p => p.MovimientoOrigenId == 1).Pendiente.ShouldBe(0m);
        partidas.Single(p => p.MovimientoOrigenId == 2).Pendiente.ShouldBe(5_700m);

        var conPago = movimientos.Append(M(3, TipoMovimientoSaldo.PagoEmpresa, -5_700m, viajeId: 10)).ToArray();
        var partidasConPago = CompensacionCuenta.Calcular(conPago);
        partidasConPago.Single(p => p.ViajeId == 10).Pendiente.ShouldBe(0m);
    }

    [Fact]
    public void DosViajesAFavor_UnPagoSaldaElMasAntiguoYDejaElRestoEnElSegundo()
    {
        var movimientos = new[]
        {
            M(1, TipoMovimientoSaldo.CierreViaje, 100_000m, viajeId: 1),
            M(2, TipoMovimientoSaldo.CierreViaje, 50_000m, viajeId: 2),
            M(3, TipoMovimientoSaldo.PagoEmpresa, -120_000m),
        };
        var partidas = CompensacionCuenta.Calcular(movimientos);

        partidas.Single(p => p.ViajeId == 1).Pendiente.ShouldBe(0m);
        partidas.Single(p => p.ViajeId == 2).Pendiente.ShouldBe(30_000m);
    }

    [Fact]
    public void ViajeEnContra_UnDescuentoNominaParcial_DejaElRestoPendiente()
    {
        var movimientos = new[]
        {
            M(1, TipoMovimientoSaldo.CierreViaje, -80_000m, viajeId: 1),
            M(2, TipoMovimientoSaldo.DescuentoNomina, 30_000m),
        };
        var partidas = CompensacionCuenta.Calcular(movimientos);

        partidas.Single(p => p.ViajeId == 1).Pendiente.ShouldBe(-50_000m);
    }

    [Fact]
    public void Reversion_AprobarReabrirYVolverAAprobar_NoDejaPartidasFantasma()
    {
        var movimientos = new[]
        {
            M(1, TipoMovimientoSaldo.Inicial, -63_500m),
            M(2, TipoMovimientoSaldo.CierreViaje, 69_200m, viajeId: 10),
            M(3, TipoMovimientoSaldo.Ajuste, -69_200m, viajeId: 10), // reversión exacta del movimiento 2
            M(4, TipoMovimientoSaldo.CierreViaje, 69_200m, viajeId: 10), // se vuelve a aprobar
        };
        var partidas = CompensacionCuenta.Calcular(movimientos);

        // Los movimientos 2 y 3 desaparecen juntos; solo quedan el Inicial y el segundo CierreViaje.
        partidas.Count(p => p.MovimientoOrigenId is 2 or 3).ShouldBe(0);
        partidas.Single(p => p.MovimientoOrigenId == 1).Pendiente.ShouldBe(0m);
        partidas.Single(p => p.MovimientoOrigenId == 4).Pendiente.ShouldBe(5_700m);
    }

    [Fact]
    public void PagoMayorQueLoPendiente_ElSobranteQuedaComoPartidaNuevaAFavorDeLaEmpresa()
    {
        var movimientos = new[]
        {
            M(1, TipoMovimientoSaldo.CierreViaje, 50_000m, viajeId: 1),
            M(2, TipoMovimientoSaldo.PagoEmpresa, -80_000m),
        };
        var partidas = CompensacionCuenta.Calcular(movimientos);

        partidas.Single(p => p.ViajeId == 1).Pendiente.ShouldBe(0m);
        var sobrante = partidas.Single(p => p.MovimientoOrigenId == 2);
        sobrante.Pendiente.ShouldBe(-30_000m); // el conductor queda debiendo el sobrante
    }
}
