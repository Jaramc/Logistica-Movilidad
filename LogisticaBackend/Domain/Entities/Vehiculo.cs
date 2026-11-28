namespace LogisticaBackend.Domain.Entities;

public class Vehiculo
{
    public int Id { get; set; }

    public string Placa { get; set; } = string.Empty;

    public string Tipo { get; set; } = "Tractomula";

    /// <summary>Categoría de peaje del vehículo. Se asigna en la fase 3; aquí siempre es null.</summary>
    public string? CategoriaPeaje { get; set; }

    public bool Activo { get; set; } = true;
}
