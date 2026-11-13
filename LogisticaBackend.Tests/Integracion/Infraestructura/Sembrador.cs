using LogisticaBackend.Auth;
using LogisticaBackend.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LogisticaBackend.Tests.Integracion.Infraestructura;

public sealed record UsuarioPrueba(Guid Id, string Email, string Password, string? ClaveTotp);

/// <summary>Crea usuarios directamente con UserManager (sin pasar por la API) para preparar escenarios.</summary>
public static class Sembrador
{
    public const string PasswordPorDefecto = "caballo bateria grapa azul";

    public static async Task<UsuarioPrueba> CrearUsuarioAsync(
        FabricaApi fabrica, string rol, bool con2fa = true, bool debeCambiarPassword = false, string? password = null)
    {
        using var scope = fabrica.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        var email = $"u{Guid.NewGuid():N}@pruebas.co";
        password ??= PasswordPorDefecto;
        var usuario = new Usuario { UserName = email, Email = email, EmailConfirmed = true, NombreCompleto = "Usuario De Prueba" };
        if (debeCambiarPassword) usuario.IniciarPasswordTemporal(DateTime.UtcNow, TimeSpan.FromHours(24));
        else usuario.FinalizarPasswordTemporal();

        var creado = await um.CreateAsync(usuario, password);
        creado.Succeeded.ShouldBeTrue(string.Join(", ", creado.Errors.Select(e => e.Description)));
        (await um.AddToRoleAsync(usuario, rol)).Succeeded.ShouldBeTrue();

        string? clave = null;
        if (con2fa)
        {
            await um.ResetAuthenticatorKeyAsync(usuario);
            clave = await um.GetAuthenticatorKeyAsync(usuario);
            await um.SetTwoFactorEnabledAsync(usuario, true);
        }

        return new UsuarioPrueba(usuario.Id, email, password, clave);
    }

    /// <summary>Cliente con sesión completa (contraseña + TOTP).</summary>
    public static async Task<(ClienteApi Cliente, UsuarioPrueba Usuario)> SesionAsync(FabricaApi fabrica, string rol)
    {
        var usuario = await CrearUsuarioAsync(fabrica, rol);
        var cliente = new ClienteApi(fabrica);
        await cliente.IniciarSesionAsync(usuario.Email, usuario.Password, usuario.ClaveTotp);
        return (cliente, usuario);
    }

    public static async Task<T> EnScopeAsync<T>(FabricaApi fabrica, Func<IServiceProvider, Task<T>> accion)
    {
        using var scope = fabrica.Services.CreateScope();
        return await accion(scope.ServiceProvider);
    }

    public static readonly string[] TodosLosRoles = Roles.Todos.ToArray();
}
