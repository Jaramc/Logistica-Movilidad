using LogisticaBackend.Domain.Entities;

namespace LogisticaBackend.Domain.Services;

/// <summary>Estado de una partida de la cuenta corriente después de compensar. ViajeId solo se llena para las partidas de un CierreViaje.</summary>
public sealed record PartidaCuenta(long MovimientoOrigenId, int? ViajeId, decimal ValorOriginal, decimal Pendiente, DateTime CreadoEn);

/// <summary>
/// Compensación FIFO por signos contrarios de las partidas de la cuenta corriente de un conductor.
/// Pura: no toca la base de datos; se recalcula siempre a partir del historial completo de
/// movimientos_saldo (nunca se guardan asignaciones). Cada Inicial, CierreViaje y Ajuste sin viaje
/// es una partida nueva que se compensa, en orden cronológico, contra la partida más antigua del
/// signo contrario. PagoEmpresa y DescuentoNomina se procesan igual que una partida nueva (su valor
/// ya viene con signo): el ViajeId de un pago es solo informativo, no decide a qué partida se aplica.
/// Un Ajuste con ViajeId es la reversión de un CierreViaje (la usa reabrir un viaje): cancela
/// exactamente esa partida antes de compensar cualquier otra cosa; las dos desaparecen juntas.
/// </summary>
public static class CompensacionCuenta
{
    private sealed class Partida
    {
        public required long MovimientoOrigenId { get; init; }
        public int? ViajeId { get; init; }
        public required decimal ValorOriginal { get; init; }
        public decimal Pendiente { get; set; }
        public required DateTime CreadoEn { get; init; }
    }

    public static IReadOnlyList<PartidaCuenta> Calcular(IEnumerable<MovimientoSaldo> movimientos)
    {
        var ordenados = movimientos.OrderBy(m => m.Id).ToList();

        // Un viaje puede tener varios CierreViaje a lo largo del tiempo si se reabre y se vuelve a
        // aprobar; cada reversión cancela el cierre vigente de SU viaje más reciente a esa fecha.
        var cierresPorViaje = ordenados
            .Where(m => m.Tipo == TipoMovimientoSaldo.CierreViaje && m.ViajeId is not null)
            .GroupBy(m => m.ViajeId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Id).ToList());
        var anulados = new HashSet<long>();
        foreach (var reversion in ordenados.Where(m => m.Tipo == TipoMovimientoSaldo.Ajuste && m.ViajeId is not null))
        {
            if (!cierresPorViaje.TryGetValue(reversion.ViajeId!.Value, out var cierres)) continue;
            var cierre = cierres.LastOrDefault(c => c.Id < reversion.Id && !anulados.Contains(c.Id));
            if (cierre is not null && cierre.Valor == -reversion.Valor)
            {
                anulados.Add(cierre.Id);
                anulados.Add(reversion.Id);
            }
        }

        var todas = new List<Partida>();
        var positivas = new Queue<Partida>();
        var negativas = new Queue<Partida>();

        void Compensar(Partida partida)
        {
            var colaOpuesta = partida.Pendiente > 0 ? negativas : positivas;
            while (partida.Pendiente != 0 && colaOpuesta.Count > 0)
            {
                var objetivo = colaOpuesta.Peek();
                var consumo = Math.Min(Math.Abs(partida.Pendiente), Math.Abs(objetivo.Pendiente));
                partida.Pendiente -= Math.Sign(partida.Pendiente) * consumo;
                objetivo.Pendiente -= Math.Sign(objetivo.Pendiente) * consumo;
                if (objetivo.Pendiente == 0) colaOpuesta.Dequeue();
            }
        }

        foreach (var m in ordenados)
        {
            if (anulados.Contains(m.Id)) continue;
            if (m.Tipo == TipoMovimientoSaldo.Ajuste && m.ViajeId is not null) continue; // reversión sin pareja exacta: no genera una partida

            var partida = new Partida
            {
                MovimientoOrigenId = m.Id,
                ViajeId = m.Tipo == TipoMovimientoSaldo.CierreViaje ? m.ViajeId : null,
                ValorOriginal = m.Valor,
                Pendiente = m.Valor,
                CreadoEn = m.CreadoEn,
            };
            Compensar(partida);
            todas.Add(partida);
            if (partida.Pendiente > 0) positivas.Enqueue(partida);
            else if (partida.Pendiente < 0) negativas.Enqueue(partida);
        }

        return todas.Select(p => new PartidaCuenta(p.MovimientoOrigenId, p.ViajeId, p.ValorOriginal, p.Pendiente, p.CreadoEn)).ToList();
    }
}
