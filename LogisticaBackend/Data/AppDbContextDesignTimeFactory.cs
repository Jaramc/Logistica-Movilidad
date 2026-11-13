using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LogisticaBackend.Data;

/// <summary>
/// Solo para las herramientas de EF (dotnet ef migrations add). Crear una migración no necesita
/// conexión real; para aplicarla (database update) se usa la variable de entorno
/// ConnectionStrings__DefaultConnection con el rol dueño (logistica_owner).
/// </summary>
public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=logistica_db;Username=logistica_owner";
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cadena).Options;
        return new AppDbContext(opciones);
    }
}
