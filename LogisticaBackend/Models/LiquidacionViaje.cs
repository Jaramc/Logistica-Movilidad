namespace LogisticaBackend.Models;

/// <summary>
/// Modelo legacy de 16 columnas fijas de gastos.
/// Tarea de retiro pendiente: Migrar a PlanillaViaje tras confirmación explícita del usuario.
/// </summary>
[Obsolete("Esquema legacy de 16 columnas fijas. Se conservará temporalmente para mantener compatibilidad con bases de datos preexistentes. Utilizar PlanillaViaje con relación 1:N a GastoDetalle.")]
public class LiquidacionViaje
{
    public int Id { get; set; }
    public DateOnly FechaInicial { get; set; }
    public DateOnly FechaFinal { get; set; }
    public string Conductor { get; set; } = string.Empty;
    public string Placa { get; set; } = string.Empty;
    public string? Manifiesto { get; set; }
    public string? EmpresaTransportadora { get; set; }
    public string? OrigenViaje { get; set; }
    public string? Destino { get; set; }

    // Gastos Operativos (16 columnas fijas heredadas)
    public decimal Peajes { get; set; } = 0m;
    public decimal Porcentaje { get; set; } = 0m;
    public decimal Comision { get; set; } = 0m;
    public decimal Parqueo { get; set; } = 0m;
    public decimal Acpm { get; set; } = 0m;
    public decimal Engrase { get; set; } = 0m;
    public decimal Tensionada { get; set; } = 0m;
    public decimal Aseo { get; set; } = 0m;
    public decimal Llantas { get; set; } = 0m;
    public decimal Urea { get; set; } = 0m;
    public decimal Cargue { get; set; } = 0m;
    public decimal Descargue { get; set; } = 0m;
    public decimal Carrosada { get; set; } = 0m;
    public decimal Descarrosada { get; set; } = 0m;
    public decimal Papeleria { get; set; } = 0m;
    public decimal Otros { get; set; } = 0m;

    // Totales y Saldos
    public decimal TotalGastos { get; set; } = 0m;
    public decimal Traslado { get; set; } = 0m;
    public decimal Basico { get; set; } = 0m;
    public decimal SaldoViaje { get; set; } = 0m;
    public decimal SaldoAnterior { get; set; } = 0m;
    public decimal SaldoTotal { get; set; } = 0m;

    // Auditoría y Control de Flujo
    public string EstadoRevision { get; set; } = "pendiente";
    public string? JsonOcrCrudo { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
}
