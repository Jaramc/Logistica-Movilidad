using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LogisticaBackend.Domain.Entities;

namespace LogisticaBackend.Data.Configurations;

public class ViajeConfiguration : IEntityTypeConfiguration<Viaje>
{
    public void Configure(EntityTypeBuilder<Viaje> builder)
    {
        builder.ToTable("viajes", t =>
        {
            t.HasCheckConstraint(RestriccionesCatalogos.FechasEnOrden, "fecha_fin IS NULL OR fecha_fin >= fecha_inicio");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();

        builder.Property(x => x.ConductorId).HasColumnName("conductor_id").IsRequired();
        builder.Property(x => x.VehiculoId).HasColumnName("vehiculo_id").IsRequired();

        builder.Property(x => x.Origen).HasColumnName("origen").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Destino).HasColumnName("destino").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Cliente).HasColumnName("cliente").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Manifiesto).HasColumnName("manifiesto").HasMaxLength(50);

        builder.Property(x => x.Flete).HasColumnName("flete").HasPrecision(14, 2).HasDefaultValue(0m).IsRequired();

        builder.Property(x => x.FechaInicio).HasColumnName("fecha_inicio").HasColumnType("date").IsRequired();
        builder.Property(x => x.FechaFin).HasColumnName("fecha_fin").HasColumnType("date");

        builder.Property(x => x.Estado).HasColumnName("estado").HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(x => x.SaldoViaje).HasColumnName("saldo_viaje").HasPrecision(14, 2);

        builder.Property(x => x.CreadoPorUsuarioId).HasColumnName("creado_por_usuario_id");
        builder.Property(x => x.AprobadoPorUsuarioId).HasColumnName("aprobado_por_usuario_id");
        builder.Property(x => x.AprobadoEn).HasColumnName("aprobado_en").HasColumnType("timestamp with time zone");

        builder.Property(x => x.Version).IsRowVersion();

        builder.HasOne(x => x.Conductor).WithMany().HasForeignKey(x => x.ConductorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Vehiculo).WithMany().HasForeignKey(x => x.VehiculoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Usuario>().WithMany().HasForeignKey(x => x.CreadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Usuario>().WithMany().HasForeignKey(x => x.AprobadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Traslados).WithOne(t => t.Viaje).HasForeignKey(t => t.ViajeId).OnDelete(DeleteBehavior.Cascade);

        // Solo un viaje Abierto o EnRevision por conductor, y otro por vehículo: evita abrir un segundo
        // viaje mientras el primero no se aprueba o se reabre.
        builder.HasIndex(x => x.ConductorId)
            .IsUnique()
            .HasDatabaseName(RestriccionesCatalogos.ConductorAbiertoUnico)
            .HasFilter("estado IN ('Abierto', 'EnRevision')");
        builder.HasIndex(x => x.VehiculoId)
            .IsUnique()
            .HasDatabaseName(RestriccionesCatalogos.VehiculoAbiertoUnico)
            .HasFilter("estado IN ('Abierto', 'EnRevision')");
    }
}
