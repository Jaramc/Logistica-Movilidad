using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LogisticaBackend.Domain.Entities;

namespace LogisticaBackend.Data.Configurations;

public class ConductorConfiguration : IEntityTypeConfiguration<Conductor>
{
    public void Configure(EntityTypeBuilder<Conductor> builder)
    {
        builder.ToTable("conductores", t =>
        {
            t.HasCheckConstraint(RestriccionesCatalogos.CedulaFormato, "cedula ~ '^[0-9]{6,12}$'");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();

        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(10).IsRequired();
        builder.Property(x => x.NombreCompleto).HasColumnName("nombre_completo").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Cedula).HasColumnName("cedula").HasMaxLength(12).IsRequired();
        builder.Property(x => x.CuentaMicrosoft).HasColumnName("cuenta_microsoft").HasMaxLength(200);
        builder.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true).IsRequired();

        builder.Property(x => x.CreadoEn)
            .HasColumnName("creado_en")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(x => x.Codigo).IsUnique().HasDatabaseName(RestriccionesCatalogos.CodigoUnico);
        builder.HasIndex(x => x.Cedula).IsUnique().HasDatabaseName(RestriccionesCatalogos.CedulaUnica);
    }
}
