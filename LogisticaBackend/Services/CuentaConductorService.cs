using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.Domain.Services;

namespace LogisticaBackend.Services;

/// <summary>Resultado de cerrar un viaje: lo que el controlador necesita para construir la respuesta y sus advertencias.</summary>
public sealed record CierreViajeResultado(
    decimal SaldoViaje,
    EstadoViaje Estado,
    decimal AnticipoTraslados,
    decimal AnticipoDeclarado);

public interface ICuentaConductorService
{
    /// <summary>Solo si el conductor no tiene ningún movimiento; si ya tiene, lanza un 409.</summary>
    Task<MovimientoSaldo> RegistrarSaldoInicialAsync(
        int conductorId, decimal valor, DateOnly fechaCorte, string justificacion, Guid? usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Calcula el saldo del viaje (TotalGastos de la planilla − Σ traslados) y crea el movimiento CierreViaje.</summary>
    Task<CierreViajeResultado> CerrarViajeAsync(int viajeId, Guid? usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Crea el PagoSaldo y su movimiento. PagoEmpresa resta, DescuentoNomina suma (ambos valores siempre positivos).</summary>
    Task<MovimientoSaldo> RegistrarPagoAsync(
        int conductorId, int? viajeId, TipoPago tipo, decimal valor, DateOnly fecha,
        string? quincena, string? medio, string? nota, Guid? usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Corrección manual con signo libre. La justificación es obligatoria.</summary>
    Task<MovimientoSaldo> RegistrarAjusteAsync(
        int conductorId, decimal valor, string justificacion, Guid? usuarioId, CancellationToken cancellationToken = default);

    Task<decimal> ObtenerSaldoAsync(int conductorId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<MovimientoSaldo> Items, int TotalCount)> ObtenerMovimientosAsync(
        int conductorId, int pagina, int tamano, CancellationToken cancellationToken = default);
}

/// <summary>
/// Único punto que escribe en movimientos_saldo. Cada escritura bloquea la fila del conductor
/// (SELECT ... FOR UPDATE) dentro de una transacción, para que dos movimientos simultáneos del
/// mismo conductor nunca partan del mismo saldo. Convención de signo: positivo = la empresa le
/// debe al conductor.
/// </summary>
public sealed class CuentaConductorService : ICuentaConductorService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _reloj;
    private readonly IAuditoriaService _auditoria;

    public CuentaConductorService(AppDbContext db, TimeProvider reloj, IAuditoriaService auditoria)
    {
        _db = db;
        _reloj = reloj;
        _auditoria = auditoria;
    }

    public async Task<MovimientoSaldo> RegistrarSaldoInicialAsync(
        int conductorId, decimal valor, DateOnly fechaCorte, string justificacion, Guid? usuarioId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(justificacion))
        {
            throw new ReglaDeNegocioException(StatusCodes.Status422UnprocessableEntity, "El saldo inicial exige una justificación.");
        }

        var movimiento = await EjecutarAsync(conductorId, async (saldoAnterior, ct) =>
        {
            if (saldoAnterior.TieneMovimientos)
            {
                throw new ReglaDeNegocioException(StatusCodes.Status409Conflict,
                    "El conductor ya tiene movimientos registrados; no se puede asignar un saldo inicial de nuevo.");
            }
            return NuevoMovimiento(conductorId, TipoMovimientoSaldo.Inicial, valor, saldoAnterior.Valor, fechaCorte, justificacion, null, null, usuarioId);
        }, cancellationToken);

        await _auditoria.RegistrarAsync(AccionesAuditoria.SaldoInicialRegistrado, true, usuarioId, "Conductor", conductorId.ToString(),
            new { conductorId, valor = movimiento.Valor, saldoResultante = movimiento.SaldoResultante }, cancellationToken);
        return movimiento;
    }

    public async Task<CierreViajeResultado> CerrarViajeAsync(int viajeId, Guid? usuarioId, CancellationToken cancellationToken = default)
    {
        var viaje = await _db.Viajes.Include(v => v.Planilla).Include(v => v.Traslados)
            .FirstOrDefaultAsync(v => v.Id == viajeId, cancellationToken);
        if (viaje is null)
        {
            throw new ReglaDeNegocioException(StatusCodes.Status404NotFound, "El viaje no existe.");
        }
        if (viaje.Estado is EstadoViaje.Aprobado or EstadoViaje.Saldado)
        {
            throw new ReglaDeNegocioException(StatusCodes.Status409Conflict, "El viaje ya fue aprobado.");
        }
        if (viaje.Planilla is null || viaje.Traslados.Count == 0)
        {
            throw new ReglaDeNegocioException(StatusCodes.Status422UnprocessableEntity,
                "El viaje debe tener una planilla guardada y al menos un traslado registrado para poder aprobarse.");
        }

        var anticipoTraslados = CalculadoraLiquidacion.Redondear(viaje.Traslados.Sum(t => t.Valor));
        var saldoViaje = CuentaConductorCalculos.SaldoViaje(viaje.Planilla.TotalGastos, anticipoTraslados);
        var fechaCorte = viaje.FechaFin ?? viaje.Planilla.FechaFinal;
        var nuevoEstado = saldoViaje == 0m ? EstadoViaje.Saldado : EstadoViaje.Aprobado;
        var ahora = _reloj.GetUtcNow().UtcDateTime;

        var movimiento = await EjecutarAsync(viaje.ConductorId, (saldoAnterior, ct) =>
            Task.FromResult(NuevoMovimiento(viaje.ConductorId, TipoMovimientoSaldo.CierreViaje, saldoViaje, saldoAnterior.Valor,
                fechaCorte, null, viajeId, null, usuarioId)),
            cancellationToken,
            antesDeGuardar: async ct =>
            {
                viaje.SaldoViaje = saldoViaje;
                viaje.FechaFin = fechaCorte;
                viaje.Estado = nuevoEstado;
                viaje.AprobadoPorUsuarioId = usuarioId;
                viaje.AprobadoEn = ahora;
                _db.Viajes.Update(viaje);
                await Task.CompletedTask;
            });

        await _auditoria.RegistrarAsync(AccionesAuditoria.ViajeCerrado, true, usuarioId, "Viaje", viajeId.ToString(),
            new { viajeId, saldoViaje, estado = nuevoEstado.ToString(), saldoResultante = movimiento.SaldoResultante }, cancellationToken);

        return new CierreViajeResultado(saldoViaje, nuevoEstado, anticipoTraslados, viaje.Planilla.Anticipo);
    }

    public async Task<MovimientoSaldo> RegistrarPagoAsync(
        int conductorId, int? viajeId, TipoPago tipo, decimal valor, DateOnly fecha,
        string? quincena, string? medio, string? nota, Guid? usuarioId, CancellationToken cancellationToken = default)
    {
        if (valor <= 0m)
        {
            throw new ReglaDeNegocioException(StatusCodes.Status422UnprocessableEntity, "El valor del pago debe ser mayor que cero.");
        }

        var valorRedondeado = CalculadoraLiquidacion.Redondear(valor);
        var valorConSigno = CuentaConductorCalculos.ValorConSigno(tipo, valor);
        var ahora = _reloj.GetUtcNow().UtcDateTime;

        var pago = new PagoSaldo
        {
            ConductorId = conductorId,
            ViajeId = viajeId,
            Tipo = tipo,
            Valor = valorRedondeado,
            Fecha = fecha,
            Quincena = quincena,
            Medio = medio,
            Nota = nota,
            RegistradoPorUsuarioId = usuarioId,
            CreadoEn = ahora,
        };

        var movimiento = await EjecutarAsync(conductorId, async (saldoAnterior, ct) =>
        {
            _db.PagosSaldo.Add(pago);
            await _db.SaveChangesAsync(ct); // asigna pago.Id, que necesita el movimiento
            var tipoMovimiento = tipo == TipoPago.PagoEmpresa ? TipoMovimientoSaldo.PagoEmpresa : TipoMovimientoSaldo.DescuentoNomina;
            return NuevoMovimiento(conductorId, tipoMovimiento, valorConSigno, saldoAnterior.Valor, fecha, null, viajeId, pago.Id, usuarioId);
        }, cancellationToken);

        await _auditoria.RegistrarAsync(AccionesAuditoria.PagoRegistrado, true, usuarioId, "PagoSaldo", pago.Id.ToString(),
            new { conductorId, viajeId, tipo = tipo.ToString(), valor = valorRedondeado, saldoResultante = movimiento.SaldoResultante }, cancellationToken);
        return movimiento;
    }

    public async Task<MovimientoSaldo> RegistrarAjusteAsync(
        int conductorId, decimal valor, string justificacion, Guid? usuarioId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(justificacion))
        {
            throw new ReglaDeNegocioException(StatusCodes.Status422UnprocessableEntity, "El ajuste exige una justificación.");
        }

        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var movimiento = await EjecutarAsync(conductorId, (saldoAnterior, ct) =>
            Task.FromResult(NuevoMovimiento(conductorId, TipoMovimientoSaldo.Ajuste, valor, saldoAnterior.Valor,
                DateOnly.FromDateTime(ahora), justificacion, null, null, usuarioId)),
            cancellationToken);

        await _auditoria.RegistrarAsync(AccionesAuditoria.AjusteRegistrado, true, usuarioId, "Conductor", conductorId.ToString(),
            new { conductorId, valor = movimiento.Valor, saldoResultante = movimiento.SaldoResultante }, cancellationToken);
        return movimiento;
    }

    public async Task<decimal> ObtenerSaldoAsync(int conductorId, CancellationToken cancellationToken = default) =>
        (await UltimoMovimientoAsync(conductorId, cancellationToken)).Valor;

    public async Task<(IReadOnlyList<MovimientoSaldo> Items, int TotalCount)> ObtenerMovimientosAsync(
        int conductorId, int pagina, int tamano, CancellationToken cancellationToken = default)
    {
        var query = _db.MovimientosSaldo.AsNoTracking().Where(m => m.ConductorId == conductorId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    private readonly record struct SaldoAnterior(decimal Valor, bool TieneMovimientos);

    private async Task<SaldoAnterior> UltimoMovimientoAsync(int conductorId, CancellationToken cancellationToken)
    {
        var ultimo = await _db.MovimientosSaldo.AsNoTracking()
            .Where(m => m.ConductorId == conductorId)
            .OrderByDescending(m => m.Id)
            .Select(m => (decimal?)m.SaldoResultante)
            .FirstOrDefaultAsync(cancellationToken);
        return new SaldoAnterior(ultimo ?? 0m, ultimo is not null);
    }

    private static MovimientoSaldo NuevoMovimiento(
        int conductorId, TipoMovimientoSaldo tipo, decimal valor, decimal saldoAnterior, DateOnly fechaCorte,
        string? justificacion, int? viajeId, int? pagoSaldoId, Guid? usuarioId) => new()
    {
        ConductorId = conductorId,
        ViajeId = viajeId,
        PagoSaldoId = pagoSaldoId,
        Tipo = tipo,
        Valor = CalculadoraLiquidacion.Redondear(valor),
        SaldoResultante = CuentaConductorCalculos.SaldoResultante(saldoAnterior, valor),
        FechaCorte = fechaCorte,
        Justificacion = justificacion,
        CreadoPorUsuarioId = usuarioId,
    };

    /// <summary>
    /// Transacción + bloqueo de la fila del conductor (SELECT ... FOR UPDATE): lee el último saldo,
    /// deja que <paramref name="construirMovimiento"/> decida el movimiento (o lance la regla de
    /// negocio que corresponda), lo inserta y hace commit. <paramref name="antesDeGuardar"/> permite
    /// aplicar cambios adicionales (p. ej. el estado del viaje) en la misma transacción.
    /// </summary>
    private async Task<MovimientoSaldo> EjecutarAsync(
        int conductorId,
        Func<SaldoAnterior, CancellationToken, Task<MovimientoSaldo>> construirMovimiento,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task>? antesDeGuardar = null)
    {
        await using var transaccion = await _db.Database.BeginTransactionAsync(cancellationToken);

        var conductor = await _db.Conductores
            .FromSqlInterpolated($"SELECT * FROM conductores WHERE id = {conductorId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (conductor is null)
        {
            throw new ReglaDeNegocioException(StatusCodes.Status404NotFound, "El conductor no existe.");
        }

        var saldoAnterior = await UltimoMovimientoAsync(conductorId, cancellationToken);
        var movimiento = await construirMovimiento(saldoAnterior, cancellationToken);

        if (antesDeGuardar is not null)
        {
            await antesDeGuardar(cancellationToken);
        }

        _db.MovimientosSaldo.Add(movimiento);
        await _db.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        return movimiento;
    }
}
