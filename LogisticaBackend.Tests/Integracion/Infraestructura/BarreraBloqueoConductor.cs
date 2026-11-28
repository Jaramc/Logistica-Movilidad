using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LogisticaBackend.Tests.Integracion.Infraestructura;

/// <summary>
/// Solo para pruebas de concurrencia. Cuando está armada, retiene el SELECT ... FOR UPDATE de la
/// fila del conductor hasta que N solicitudes hayan llegado a ese punto. Así todas ya leyeron el
/// viaje (con el mismo xmin) antes de que cualquiera escriba, y el choque no depende del tiempo.
/// Desarmada no hace nada. Las pruebas de la colección corren en serie, así que es seguro compartirla.
/// </summary>
public sealed class BarreraBloqueoConductor : DbCommandInterceptor
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(10);

    private TaskCompletionSource? _todasLlegaron;
    private int _esperadas;
    private int _llegadas;

    public void Armar(int solicitudes)
    {
        _esperadas = solicitudes;
        _llegadas = 0;
        _todasLlegaron = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Desarmar() => _todasLlegaron = null;

    /// <summary>Cuántas solicitudes retuvo la barrera desde que se armó (para comprobar que actuó).</summary>
    public int Llegadas => Volatile.Read(ref _llegadas);

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        var barrera = _todasLlegaron;
        if (barrera is not null
            && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)
            && command.CommandText.Contains("conductores", StringComparison.Ordinal))
        {
            if (Interlocked.Increment(ref _llegadas) >= _esperadas)
            {
                _todasLlegaron = null;
                barrera.TrySetResult();
            }
            await barrera.Task.WaitAsync(Espera, cancellationToken);
        }
        return result;
    }
}
