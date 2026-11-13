using System.Security.Cryptography;
using System.Text;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace LogisticaBackend.Auth;

/// <summary>
/// Store de Identity que guarda los códigos de recuperación 2FA como hash SHA-256
/// (Identity los guarda en texto plano por defecto). Un volcado de la base de datos
/// no permite usar los códigos. Son aleatorios y de alta entropía, por eso basta SHA-256.
/// </summary>
public class UsuarioStore : UserStore<Usuario, IdentityRole<Guid>, AppDbContext, Guid>
{
    public UsuarioStore(AppDbContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
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
