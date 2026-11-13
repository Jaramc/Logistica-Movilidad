using LogisticaBackend.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace LogisticaBackend.Auth;

/// <summary>
/// Verifica la contraseña contra un hash ficticio para que un login con un correo inexistente,
/// bloqueado o inactivo tarde lo mismo que uno con un usuario real (evita enumerar usuarios por tiempo).
/// </summary>
public static class HashFicticio
{
    private static readonly Usuario UsuarioFicticio = new() { UserName = "ficticio" };
    private static string? _hash;

    public static void Verificar(IPasswordHasher<Usuario> hasher, string password)
    {
        _hash ??= hasher.HashPassword(UsuarioFicticio, Guid.NewGuid().ToString("N"));
        hasher.VerifyHashedPassword(UsuarioFicticio, _hash, password);
    }
}
