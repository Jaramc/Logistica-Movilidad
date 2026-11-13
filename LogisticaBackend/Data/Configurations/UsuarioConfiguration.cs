using LogisticaBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticaBackend.Data.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");

        builder.Property(x => x.NombreCompleto).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Activo).HasDefaultValue(true);
        builder.Property(x => x.DebeCambiarPassword).HasDefaultValue(true);
        builder.Property(x => x.CreadoEn).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UltimoAcceso).HasColumnType("timestamp with time zone");

        // Correo único también a nivel de base de datos (Identity ya lo valida en la aplicación).
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
    }
}
