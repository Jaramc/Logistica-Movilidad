using System.Security.Cryptography;
using System.Text;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace LogisticaBackend.Auth;

/// <summary>
/// Store de Identity endurecido:
/// - la clave TOTP se guarda CIFRADA con ASP.NET Data Protection (las llaves viven en disco, en
///   Seguridad:RutaClavesDataProtection, nunca en la base de datos). Un volcado de la base no
///   permite generar códigos 2FA;
/// - los códigos de recuperación se guardan como hash SHA-256 (Identity los guarda en texto plano).
/// </summary>
public class UsuarioStore : UserStore<Usuario, IdentityRole<Guid>, AppDbContext, Guid>
{
    public const string PropositoClaveTotp = "Logistica.Identity.ClaveTotp.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<UsuarioStore> _logger;

    public UsuarioStore(
        AppDbContext context,
        IDataProtectionProvider dataProtection,
        ILogger<UsuarioStore> logger,
        IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
        _protector = dataProtection.CreateProtector(PropositoClaveTotp);
        _logger = logger;
    }

    public override Task SetAuthenticatorKeyAsync(Usuario user, string key, CancellationToken cancellationToken)
        => base.SetAuthenticatorKeyAsync(user, _protector.Protect(key), cancellationToken);

    public override async Task<string?> GetAuthenticatorKeyAsync(Usuario user, CancellationToken cancellationToken)
    {
        var cifrada = await base.GetAuthenticatorKeyAsync(user, cancellationToken);
        if (string.IsNullOrEmpty(cifrada))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(cifrada);
        }
        catch (CryptographicException)
        {
            // Llaves de Data Protection perdidas o cambiadas: el 2FA del usuario debe reiniciarse.
            _logger.LogError("No se pudo descifrar la clave TOTP del usuario {UsuarioId}. ¿Se perdieron las llaves de Data Protection?", user.Id);
            return null;
        }
    }

    public override Task ReplaceCodesAsync(Usuario user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken = default)
        => base.ReplaceCodesAsync(user, recoveryCodes.Select(HashCodigo), cancellationToken);

    public override Task<bool> RedeemCodeAsync(Usuario user, string code, CancellationToken cancellationToken = default)
        => base.RedeemCodeAsync(user, HashCodigo(code), cancellationToken);

    public static string NormalizarCodigo(string codigo) =>
        new string(codigo.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    private static string HashCodigo(string codigo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizarCodigo(codigo))));
}
