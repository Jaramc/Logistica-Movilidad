using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LogisticaBackend.Domain.Entities;

namespace LogisticaBackend.Data.Configurations;

public class TrasladoConfiguration : IEntityTypeConfiguration<Traslado>
{
    public void Configure(EntityTypeBuilder<Traslado> builder)
    {
        builder.ToTable("traslados", t =>
        {
            t.HasCheckConstraint(RestriccionesCatalogos.TrasladoValorPositivo, "valor > 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();

        builder.Property(x => x.ViajeId).HasColumnName("viaje_id").IsRequired();
        builder.Property(x => x.Fecha).HasColumnName("fecha").HasColumnType("date").IsRequired();
        builder.Property(x => x.Valor).HasColumnName("valor").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.Medio).HasColumnName("medio").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Nota).HasColumnName("nota").HasMaxLength(200);
        builder.Property(x => x.RegistradoPorUsuarioId).HasColumnName("registrado_por_usuario_id");

        builder.Property(x => x.CreadoEn)
            .HasColumnName("creado_en")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne<Domain.Entities.Usuario>().WithMany().HasForeignKey(x => x.RegistradoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ViajeId).HasDatabaseName("ix_traslados_viaje_id");
    }
}
