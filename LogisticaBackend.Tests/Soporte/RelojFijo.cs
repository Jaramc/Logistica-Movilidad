namespace LogisticaBackend.Tests.Soporte;

/// <summary>Reloj controlable para pruebas.</summary>
public sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = ahora;
    public override DateTimeOffset GetUtcNow() => Ahora;
    public void Avanzar(TimeSpan tiempo) => Ahora += tiempo;
}
