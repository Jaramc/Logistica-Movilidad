using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.Data.Configurations;

namespace LogisticaBackend.Data;

public class AppDbContext : IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<PlanillaViaje> Planillas => Set<PlanillaViaje>();
    public DbSet<GastoDetalle> GastosDetalle => Set<GastoDetalle>();

    /// <summary>Solo inserción (ver RegistroAuditoria).</summary>
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new PlanillaViajeConfiguration());
        modelBuilder.ApplyConfiguration(new GastoDetalleConfiguration());
        modelBuilder.ApplyConfiguration(new UsuarioConfiguration());
        modelBuilder.ApplyConfiguration(new RegistroAuditoriaConfiguration());

        // Tablas de Identity con nombres en español y snake_case.
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("usuarios_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("usuarios_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("usuarios_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("usuarios_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("roles_claims");
    }
}
