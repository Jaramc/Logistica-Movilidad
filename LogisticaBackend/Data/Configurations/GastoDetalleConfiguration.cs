using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LogisticaBackend.Domain.Entities;

namespace LogisticaBackend.Data.Configurations;

public class GastoDetalleConfiguration : IEntityTypeConfiguration<GastoDetalle>
{
    public void Configure(EntityTypeBuilder<GastoDetalle> builder)
    {
        builder.ToTable("gastos_detalle", t =>
        {
            t.HasCheckConstraint(RestriccionesPlanilla.ValorNoNegativo, "valor >= 0");
            t.HasCheckConstraint(RestriccionesPlanilla.CategoriaValida, $"concepto_canonico IN ({RestriccionesPlanilla.CategoriasSql})");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();

        builder.Property(x => x.PlanillaViajeId)
            .HasColumnName("planilla_viaje_id")
            .IsRequired();

        builder.Property(x => x.Concepto)
            .HasColumnName("concepto")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.ConceptoCanonico)
            .HasColumnName("concepto_canonico")
            .HasMaxLength(50)
            .HasDefaultValue("OTROS")
            .IsRequired();

        builder.Property(x => x.Etiqueta).HasColumnName("etiqueta").HasMaxLength(60);
        builder.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(200);

        builder.Property(x => x.Valor)
            .HasColumnName("valor")
            .HasPrecision(14, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.Orden)
            .HasColumnName("orden")
            .HasDefaultValue(0);

        builder.HasIndex(x => x.PlanillaViajeId)
            .HasDatabaseName("ix_gastos_detalle_planilla_viaje_id");

        builder.HasIndex(x => x.ConceptoCanonico)
            .HasDatabaseName("ix_gastos_detalle_concepto_canonico");
    }
}

