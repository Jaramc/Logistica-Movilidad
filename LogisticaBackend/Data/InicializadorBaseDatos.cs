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
        var reloj = sp.GetRequiredService<TimeProvider>();
        var vigencia = configuration.GetValue("Seguridad:VigenciaPasswordTemporal", TimeSpan.FromHours(24));
        var email = configuration[VariableEmail];
        var password = configuration[VariablePassword];

        if (await userManager.Users.AnyAsync(cancellationToken))
        {
            await RenovarAdministradorInicialVencidoAsync(userManager, reloj, vigencia, email, password, logger, cancellationToken);
            return;
        }

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
        };
        // Contraseña temporal de 24 h y un solo uso; sin 2FA: deberá configurarlo antes de usar el sistema.
        admin.IniciarPasswordTemporal(reloj.GetUtcNow().UtcDateTime, vigencia);

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

    /// <summary>
    /// Si el único usuario es el administrador inicial, no completó la activación (2FA y cambio de
    /// contraseña) y su contraseña temporal venció o se usó, nadie podría generarle otra. En ese caso,
    /// y solo si ADMIN_INICIAL_EMAIL coincide y ADMIN_INICIAL_PASSWORD está definida, se renueva.
    /// </summary>
    private static async Task RenovarAdministradorInicialVencidoAsync(UserManager<Usuario> userManager, TimeProvider reloj,
        TimeSpan vigencia, string? email, string? password, ILogger logger, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        if (await userManager.Users.CountAsync(cancellationToken) != 1) return;

        var admin = await userManager.FindByEmailAsync(email.Trim());
        var ahora = reloj.GetUtcNow().UtcDateTime;
        // Activación incompleta = sin 2FA o con la contraseña temporal aún sin cambiar. Si configuró el 2FA
        // pero perdió la sesión antes de cambiar la contraseña, la temporal ya está consumida y nadie más
        // puede generarle otra: también se renueva (conserva su 2FA ya configurado).
        var activacionIncompleta = admin is not null && (admin.PendienteActivacion || admin.DebeCambiarPassword);
        if (admin is null || !activacionIncompleta || admin.PasswordTemporalVigente(ahora)) return;

        var token = await userManager.GeneratePasswordResetTokenAsync(admin);
        var resultado = await userManager.ResetPasswordAsync(admin, token, password);
        if (!resultado.Succeeded)
        {
            logger.LogError("No se pudo renovar la contraseña del administrador inicial: {Errores}",
                string.Join(", ", resultado.Errors.Select(e => e.Code)));
            return;
        }
        admin.IniciarPasswordTemporal(ahora, vigencia);
        await userManager.UpdateAsync(admin);
        logger.LogWarning("Contraseña temporal del administrador inicial renovada por 24 horas.");
    }
}
