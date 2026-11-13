namespace LogisticaBackend.Domain.Entities;

/// <summary>
/// Evento de auditoría. Tabla de SOLO inserción: la aplicación nunca actualiza ni borra filas
/// (el servicio solo inserta, no hay endpoints de edición, el usuario de base de datos no tiene
/// UPDATE/DELETE y un trigger lo impide). El detalle nunca lleva datos sensibles.
/// </summary>
public class RegistroAuditoria
{
    public long Id { get; set; }
    public DateTime FechaUtc { get; set; }
    public Guid? UsuarioId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string? Entidad { get; set; }
    public string? EntidadId { get; set; }
    public string? Ip { get; set; }
    /// <summary>JSON (jsonb) sin contraseñas, códigos, cookies ni contenido de archivos.</summary>
    public string? Detalle { get; set; }
    public bool Exito { get; set; }
}
