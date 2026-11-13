using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Models;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.Data.Configurations;

namespace LogisticaBackend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Esquema nuevo y normalizado con relación 1:N para gastos dinámicos.
    /// </summary>
    public DbSet<PlanillaViaje> Planillas => Set<PlanillaViaje>();
    public DbSet<GastoDetalle> GastosDetalle => Set<GastoDetalle>();

    /// <summary>
    /// Esquema legacy conservado temporalmente para no romper bases de datos preexistentes.
    /// </summary>
    [Obsolete("Usar DbSet<PlanillaViaje> Planillas.")]
    public DbSet<LiquidacionViaje> LiquidacionesViaje => Set<LiquidacionViaje>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuraciones de entidades
        modelBuilder.ApplyConfiguration(new PlanillaViajeConfiguration());
        modelBuilder.ApplyConfiguration(new GastoDetalleConfiguration());
        modelBuilder.ApplyConfiguration(new LiquidacionViajeConfiguration());
    }
}
