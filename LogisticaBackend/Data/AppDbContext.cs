using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.Data.Configurations;

namespace LogisticaBackend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<PlanillaViaje> Planillas => Set<PlanillaViaje>();
    public DbSet<GastoDetalle> GastosDetalle => Set<GastoDetalle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new PlanillaViajeConfiguration());
        modelBuilder.ApplyConfiguration(new GastoDetalleConfiguration());
    }
}
