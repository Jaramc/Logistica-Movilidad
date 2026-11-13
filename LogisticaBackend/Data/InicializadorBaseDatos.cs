using LogisticaBackend.Auth;
using LogisticaBackend.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LogisticaBackend.Data;

/// <summary>
/// Al arrancar: aplica migraciones (si BaseDatos:AplicarMigraciones = true), crea los roles
/// y, solo si no existe ningún usuario, crea el primer Administrador a partir de
/// ADMIN_INICIAL_EMAIL y ADMIN_INICIAL_PASSWORD. Ninguna contraseña está escrita en el código.
/// </summary>
public static class InicializadorBaseDatos
{
    public const string VariableEmail = "ADMIN_INICIAL_EMAIL";
    public const string VariablePassword = "ADMIN_INICIAL_PASSWORD";

    public static async Task InicializarAsync(IServiceProvider servicios, CancellationToken cancellationToken = default)
    {
        using var scope = servicios.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InicializadorBaseDatos));
        var configuration = sp.GetRequiredService<IConfiguration>();
        var db = sp.GetRequiredService<AppDbContext>();

        if (configuration.GetValue("BaseDatos:AplicarMigraciones", false))
        {
            logger.LogInformation("Aplicando migraciones pendientes.");
            await db.Database.MigrateAsync(cancellationToken);
        }

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var rol in Roles.Todos)
        {
            if (!await roleManager.RoleExistsAsync(rol))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(rol));
            }
        }

        var userManager = sp.GetRequiredService<UserManager<Usuario>>();
        if (await userManager.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var email = configuration[VariableEmail];
        var password = configuration[VariablePassword];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No hay usuarios. Defina {VarEmail} y {VarPassword} para crear el primer administrador.",
                VariableEmail, VariablePassword);
            return;
        }

        var admin = new Usuario
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            EmailConfirmed = true,
            NombreCompleto = "Administrador inicial",
            Activo = true,
            DebeCambiarPassword = true, // y sin 2FA: deberá configurarlo antes de usar el sistema
        };

        var resultado = await userManager.CreateAsync(admin, password);
        if (!resultado.Succeeded)
        {
            // Solo se registran los códigos de error, nunca la contraseña.
            logger.LogError("No se pudo crear el administrador inicial: {Errores}",
                string.Join(", ", resultado.Errors.Select(e => e.Code)));
            return;
        }
        await userManager.AddToRoleAsync(admin, Roles.Administrador);
        logger.LogWarning("Administrador inicial creado. Elimine {VarPassword} del entorno; deberá cambiar la contraseña y configurar 2FA.",
            VariablePassword);
    }
}
