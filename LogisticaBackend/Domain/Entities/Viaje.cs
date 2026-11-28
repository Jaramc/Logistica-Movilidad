namespace LogisticaBackend.Domain.Entities;

/// <summary>
/// Un viaje de un conductor con un vehículo. Solo puede haber un viaje Abierto o EnRevision por
/// conductor y otro por vehículo a la vez (índices únicos parciales en la configuración).
/// </summary>
public class Viaje
{
    public int Id { get; set; }

    public int ConductorId { get; set; }
    public Conductor? Conductor { get; set; }

    public int VehiculoId { get; set; }
    public Vehiculo? Vehiculo { get; set; }

    public string Origen { get; set; } = string.Empty;
    public string Destino { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string? Manifiesto { get; set; }

    public decimal Flete { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }

    public EstadoViaje Estado { get; set; } = EstadoViaje.Abierto;

    /// <summary>Saldo del viaje (= Σ gastos aceptados − anticipo). Null hasta que se aprueba.</summary>
    public decimal? SaldoViaje { get; set; }

    /// <summary>Usuario que abrió el viaje. Se toma de la sesión, nunca del cuerpo de la petición.</summary>
    public Guid? CreadoPorUsuarioId { get; set; }

    /// <summary>Usuario que aprobó el viaje. Se toma de la sesión, nunca del cuerpo de la petición.</summary>
    public Guid? AprobadoPorUsuarioId { get; set; }
    public DateTime? AprobadoEn { get; set; }

    /// <summary>Token de concurrencia optimista nativo de PostgreSQL (columna oculta de sistema xmin).</summary>
    public uint Version { get; set; }

    public ICollection<Traslado> Traslados { get; set; } = new List<Traslado>();
    public PlanillaViaje? Planilla { get; set; }
}
