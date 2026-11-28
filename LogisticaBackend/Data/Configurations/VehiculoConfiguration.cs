using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LogisticaBackend.Domain.Entities;

namespace LogisticaBackend.Data.Configurations;

public class VehiculoConfiguration : IEntityTypeConfiguration<Vehiculo>
{
    public void Configure(EntityTypeBuilder<Vehiculo> builder)
    {
        builder.ToTable("vehiculos", t =>
        {
            t.HasCheckConstraint(RestriccionesCatalogos.PlacaFormato, "placa ~ '^[A-Z]{3}[0-9]{3}$'");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();

        builder.Property(x => x.Placa).HasColumnName("placa").HasMaxLength(6).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(30).HasDefaultValue("Tractomula").IsRequired();
        builder.Property(x => x.CategoriaPeaje).HasColumnName("categoria_peaje").HasMaxLength(30);
        builder.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true).IsRequired();

        builder.HasIndex(x => x.Placa).IsUnique().HasDatabaseName(RestriccionesCatalogos.PlacaUnica);
    }
}
