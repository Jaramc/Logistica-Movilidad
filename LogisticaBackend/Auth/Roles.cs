namespace LogisticaBackend.Auth;

/// <summary>
/// Roles del sistema. Para agregar uno nuevo (p. ej. Conductor para la app móvil):
/// 1) agregue la constante y súmela a <see cref="Todos"/>;
/// 2) cree una política en <see cref="Politicas"/> o súmelo a las existentes;
/// 3) el inicializador lo crea en la base de datos al arrancar.
/// </summary>
public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Revisor = "Revisor";
    public const string Consulta = "Consulta";

    public static readonly IReadOnlyList<string> Todos = new[] { Administrador, Revisor, Consulta };

    public static bool EsValido(string? rol) => rol is not null && Todos.Contains(rol);
}
