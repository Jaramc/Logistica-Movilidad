namespace LogisticaBackend.DTOs;

/// <summary>
/// DTO con la información consolidada y extraída de la Planilla de Gastos del Conductor.
/// </summary>
public class PlanillaOcrResultDto
{
    public string? Placa { get; set; }
    public string? Origen { get; set; }
    public string? Destino { get; set; }
    public double Flete { get; set; } = 0.0;
    public double Anticipo { get; set; } = 0.0;
    public double TotalGastos { get; set; } = 0.0;
    public double SaldoViaje { get; set; } = 0.0;

    /// <summary>
    /// Filas de la tabla dinámica 'DetalleGastos' identificadas en el documento.
    /// </summary>
    public List<GastoDetalleDto> DetalleGastos { get; set; } = new();
}

/// <summary>
/// DTO que representa un renglón individual dentro de la tabla de gastos.
/// </summary>
public class GastoDetalleDto
{
    public string Concepto { get; set; } = string.Empty;
    public double Valor { get; set; } = 0.0;
}

