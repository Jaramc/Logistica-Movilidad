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

    /// <summary>
    /// Revierte el CierreViaje de un viaje aprobado (crea un Ajuste exactamente contrario, ligado al
    /// viaje) y lo deja Abierto. Solo si el viaje no tiene pagos ligados ni el conductor tiene pagos
    /// (PagoEmpresa o DescuentoNomina) registrados después del CierreViaje que se revierte.
    /// </summary>
    Task<MovimientoSaldo> ReabrirViajeAsync(int viajeId, string motivo, Guid? usuarioId, CancellationToken cancellationToken = default);

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

    /// <summary>Partidas con pendiente distinto de cero (Inicial, CierreViaje y Ajuste sin viaje), de la más antigua a la más nueva.</summary>
    Task<IReadOnlyList<PartidaCuenta>> ObtenerPartidasPendientesAsync(int conductorId, CancellationToken cancellationToken = default);

    /// <summary>Pendiente de la partida CierreViaje de un viaje concreto. Null si el viaje nunca se aprobó (sin CierreViaje).</summary>
    Task<decimal?> ObtenerPendienteViajeAsync(int viajeId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Único punto que escribe en movimientos_saldo. Cada escritura bloquea la fila del conductor
/// (SELECT ... FOR UPDATE) dentro de una transacción, para que dos movimientos simultáneos del
/// mismo conductor nunca partan del mismo saldo. Convención de signo: positivo = la empresa le
/// debe al conductor. Después de cada movimiento se recalcula la compensación (CompensacionCuenta)
/// y se sincroniza el estado (Aprobado/Saldado) de los viajes del conductor.
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
        var ahora = _reloj.GetUtcNow().UtcDateTime;

        await EjecutarAsync(viaje.ConductorId, (saldoAnterior, ct) =>
            Task.FromResult(NuevoMovimiento(viaje.ConductorId, TipoMovimientoSaldo.CierreViaje, saldoViaje, saldoAnterior.Valor,
                fechaCorte, null, viajeId, null, usuarioId)),
            cancellationToken,
            antesDeGuardar: async ct =>
            {
                viaje.SaldoViaje = saldoViaje;
                viaje.FechaFin = fechaCorte;
                // Provisional: si la compensación deja la partida en 0, la sincronización la sube a Saldado.
                viaje.Estado = EstadoViaje.Aprobado;
                viaje.AprobadoPorUsuarioId = usuarioId;
                viaje.AprobadoEn = ahora;
                await Task.CompletedTask;
            });

        // viaje sigue siendo la misma instancia rastreada: SincronizarEstadosViajesAsync ya actualizó
        // su Estado (a Saldado) si la compensación dejó la partida en 0.
        await _auditoria.RegistrarAsync(AccionesAuditoria.ViajeCerrado, true, usuarioId, "Viaje", viajeId.ToString(),
            new { viajeId, saldoViaje, estado = viaje.Estado.ToString() }, cancellationToken);

        return new CierreViajeResultado(saldoViaje, viaje.Estado, anticipoTraslados, viaje.Planilla.Anticipo);
    }

    public async Task<MovimientoSaldo> ReabrirViajeAsync(int viajeId, string motivo, Guid? usuarioId, CancellationToken cancellationToken = default)
    {
        var viaje = await _db.Viajes.FirstOrDefaultAsync(v => v.Id == viajeId, cancellationToken);
        if (viaje is null)
        {
            throw new ReglaDeNegocioException(StatusCodes.Status404NotFound, "El viaje no existe.");
        }
        if (viaje.Estado is not (EstadoViaje.Aprobado or EstadoViaje.Saldado))
        {
            throw new ReglaDeNegocioException(StatusCodes.Status409Conflict, "El viaje no está aprobado; no hay nada que reabrir.");
        }
        if (await _db.PagosSaldo.AnyAsync(p => p.ViajeId == viajeId, cancellationToken))
        {
            throw new ReglaDeNegocioException(StatusCodes.Status409Conflict, "El viaje ya tiene pagos registrados; no se puede reabrir.");
        }

        var cierre = await _db.MovimientosSaldo.AsNoTracking()
            .Where(m => m.ViajeId == viajeId && m.Tipo == TipoMovimientoSaldo.CierreViaje)
            .OrderByDescending(m => m.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (cierre is null)
        {
            throw new ReglaDeNegocioException(StatusCodes.Status409Conflict, "No se encontró el cierre del viaje a revertir.");
        }

        var justificacion = $"Reversión del cierre del viaje #{viajeId} (movimiento #{cierre.Id}): {motivo}";
        var movimiento = await EjecutarAsync(viaje.ConductorId, async (saldoAnterior, ct) =>
        {
            // Con la compensación FIFO el ViajeId del pago no decide a qué partida se aplica: cualquier
            // pago posterior al cierre pudo haberlo saldado. Se revisa dentro del bloqueo del conductor.
            var conductorId = viaje.ConductorId;
            var hayPagosPosteriores = await _db.MovimientosSaldo.AsNoTracking().AnyAsync(m =>
                m.ConductorId == conductorId
                && m.Id > cierre.Id
                && (m.Tipo == TipoMovimientoSaldo.PagoEmpresa || m.Tipo == TipoMovimientoSaldo.DescuentoNomina), ct);
            if (hayPagosPosteriores)
            {
                throw new ReglaDeNegocioException(StatusCodes.Status409Conflict,
                    "Hay pagos registrados después de la aprobación de este viaje; no se puede reabrir. Registra un ajuste si hace falta corregir.");
            }
            return NuevoMovimiento(viaje.ConductorId, TipoMovimientoSaldo.Ajuste, -cierre.Valor, saldoAnterior.Valor,
                DateOnly.FromDateTime(_reloj.GetUtcNow().UtcDateTime), justificacion, viajeId, null, usuarioId);
        },
            cancellationToken,
            antesDeGuardar: async ct =>
            {
                viaje.Estado = EstadoViaje.Abierto;
                viaje.SaldoViaje = null;
                viaje.AprobadoEn = null;
                viaje.AprobadoPorUsuarioId = null;
                await Task.CompletedTask;
            });

        await _auditoria.RegistrarAsync(AccionesAuditoria.ViajeReabierto, true, usuarioId, "Viaje", viajeId.ToString(),
            new { viajeId, motivo, movimientoCierreId = cierre.Id }, cancellationToken);
        return movimiento;
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

    public async Task<IReadOnlyList<PartidaCuenta>> ObtenerPartidasPendientesAsync(int conductorId, CancellationToken cancellationToken = default)
    {
        var movimientos = await _db.MovimientosSaldo.AsNoTracking().Where(m => m.ConductorId == conductorId).ToListAsync(cancellationToken);
        return CompensacionCuenta.Calcular(movimientos).Where(p => p.Pendiente != 0m).OrderBy(p => p.CreadoEn).ToList();
    }

    public async Task<decimal?> ObtenerPendienteViajeAsync(int viajeId, CancellationToken cancellationToken = default)
    {
        var conductorId = await _db.Viajes.AsNoTracking().Where(v => v.Id == viajeId).Select(v => v.ConductorId).FirstOrDefaultAsync(cancellationToken);
        var movimientos = await _db.MovimientosSaldo.AsNoTracking().Where(m => m.ConductorId == conductorId).ToListAsync(cancellationToken);
        var partida = CompensacionCuenta.Calcular(movimientos).LastOrDefault(p => p.ViajeId == viajeId);
        return partida?.Pendiente;
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
    /// negocio que corresponda), lo inserta, sincroniza el estado de los viajes del conductor según
    /// la compensación resultante y hace commit. <paramref name="antesDeGuardar"/> permite aplicar
    /// cambios adicionales (p. ej. el estado del viaje que originó el movimiento) en la misma transacción.
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

        await SincronizarEstadosViajesAsync(conductorId, cancellationToken);

        await transaccion.CommitAsync(cancellationToken);
        return movimiento;
    }

    /// <summary>
    /// Tras cada movimiento, recalcula la compensación y pone en Saldado los viajes (Aprobado o
    /// Saldado) cuya partida CierreViaje quedó en 0, o los regresa a Aprobado si volvió a quedar
    /// pendiente (p. ej. por una reversión de otro viaje que libera cola). Los viajes Abiertos o
    /// EnRevision no tienen partida y no se tocan aquí.
    /// </summary>
    private async Task SincronizarEstadosViajesAsync(int conductorId, CancellationToken cancellationToken)
    {
        var movimientos = await _db.MovimientosSaldo.AsNoTracking().Where(m => m.ConductorId == conductorId).ToListAsync(cancellationToken);
        var pendientePorViaje = CompensacionCuenta.Calcular(movimientos)
            .Where(p => p.ViajeId is not null)
            .ToDictionary(p => p.ViajeId!.Value, p => p.Pendiente);

        var viajes = await _db.Viajes
            .Where(v => v.ConductorId == conductorId && (v.Estado == EstadoViaje.Aprobado || v.Estado == EstadoViaje.Saldado))
            .ToListAsync(cancellationToken);

        var cambios = false;
        foreach (var viaje in viajes)
        {
            if (!pendientePorViaje.TryGetValue(viaje.Id, out var pendiente)) continue;
            var nuevoEstado = pendiente == 0m ? EstadoViaje.Saldado : EstadoViaje.Aprobado;
            if (viaje.Estado != nuevoEstado)
            {
                viaje.Estado = nuevoEstado;
                cambios = true;
            }
        }
        if (cambios)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
