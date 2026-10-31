using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Models;
using LogisticaBackend.Data.Configurations;

namespace LogisticaBackend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<LiquidacionViaje> LiquidacionesViaje => Set<LiquidacionViaje>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new LiquidacionViajeConfiguration());
    }
}
