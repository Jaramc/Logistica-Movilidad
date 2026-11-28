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

    public DbSet<Conductor> Conductores => Set<Conductor>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<Viaje> Viajes => Set<Viaje>();
    public DbSet<Traslado> Traslados => Set<Traslado>();
    public DbSet<PagoSaldo> PagosSaldo => Set<PagoSaldo>();

    /// <summary>Solo inserción (ver MovimientoSaldo).</summary>
    public DbSet<MovimientoSaldo> MovimientosSaldo => Set<MovimientoSaldo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new PlanillaViajeConfiguration());
        modelBuilder.ApplyConfiguration(new GastoDetalleConfiguration());
        modelBuilder.ApplyConfiguration(new UsuarioConfiguration());
        modelBuilder.ApplyConfiguration(new RegistroAuditoriaConfiguration());
        modelBuilder.ApplyConfiguration(new ConductorConfiguration());
        modelBuilder.ApplyConfiguration(new VehiculoConfiguration());
        modelBuilder.ApplyConfiguration(new ViajeConfiguration());
        modelBuilder.ApplyConfiguration(new TrasladoConfiguration());
        modelBuilder.ApplyConfiguration(new PagoSaldoConfiguration());
        modelBuilder.ApplyConfiguration(new MovimientoSaldoConfiguration());

        // Código corto del conductor (C01, C02, …): una secuencia evita condiciones de carrera al
        // crear conductores en paralelo; ConductoresController la usa para formatear el código.
        modelBuilder.HasSequence<int>("seq_conductores_codigo").StartsAt(1);

        // Las 4 placas reales se siembran aquí; los conductores reales los crea la administradora
        // desde el panel (el repositorio es público, no se siembran cédulas ni nombres reales).
        modelBuilder.Entity<Vehiculo>().HasData(
            new Vehiculo { Id = 1, Placa = "JYP108", Tipo = "Tractomula", CategoriaPeaje = null, Activo = true },
            new Vehiculo { Id = 2, Placa = "JYP052", Tipo = "Tractomula", CategoriaPeaje = null, Activo = true },
            new Vehiculo { Id = 3, Placa = "NNN977", Tipo = "Tractomula", CategoriaPeaje = null, Activo = true },
            new Vehiculo { Id = 4, Placa = "STW514", Tipo = "Tractomula", CategoriaPeaje = null, Activo = true });

        // Tablas de Identity con nombres en español y snake_case.
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("usuarios_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("usuarios_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("usuarios_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("usuarios_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("roles_claims");
    }
}
