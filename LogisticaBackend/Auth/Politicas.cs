using Microsoft.AspNetCore.Authorization;

namespace LogisticaBackend.Auth;

/// <summary>
/// Políticas de autorización. Todo está cerrado por defecto: la FallbackPolicy exige
/// sesión completa (segundo factor y contraseña definitiva) en cualquier endpoint sin atributo.
/// </summary>
public static class Politicas
{
    /// <summary>Sesión iniciada aunque falte configurar 2FA o cambiar la contraseña.
    /// Solo para: me, logout, cambiar-password, 2fa/configurar, 2fa/verificar.</summary>
    public const string SesionIniciada = nameof(SesionIniciada);

    public const string PuedeLeer = nameof(PuedeLeer);
    public const string PuedeRevisar = nameof(PuedeRevisar);
    public const string SoloAdministrador = nameof(SoloAdministrador);

    /// <summary>Requisitos de una sesión completa: autenticado, con MFA y sin contraseña temporal.</summary>
    public static AuthorizationPolicyBuilder SesionCompleta(this AuthorizationPolicyBuilder p) =>
        p.RequireAuthenticatedUser()
         .RequireClaim(ClaimsSeguridad.Amr, ClaimsSeguridad.AmrMfa)
         .RequireAssertion(ctx => !ctx.User.HasClaim(ClaimsSeguridad.DebeCambiarPassword, "true"));

    public static void Registrar(AuthorizationOptions opciones)
    {
        opciones.FallbackPolicy = new AuthorizationPolicyBuilder().SesionCompleta().Build();
        opciones.DefaultPolicy = opciones.FallbackPolicy;

        opciones.AddPolicy(SesionIniciada, p => p.RequireAuthenticatedUser());

        opciones.AddPolicy(PuedeLeer, p => p.SesionCompleta()
            .RequireRole(Roles.Administrador, Roles.Revisor, Roles.Consulta));
        opciones.AddPolicy(PuedeRevisar, p => p.SesionCompleta()
            .RequireRole(Roles.Administrador, Roles.Revisor));
        opciones.AddPolicy(SoloAdministrador, p => p.SesionCompleta()
            .RequireRole(Roles.Administrador));
    }
}
