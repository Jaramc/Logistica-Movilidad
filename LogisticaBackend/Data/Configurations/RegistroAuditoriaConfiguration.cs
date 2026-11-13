using LogisticaBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticaBackend.Data.Configurations;

public class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("auditoria");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(x => x.FechaUtc).HasColumnName("fecha_utc").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        builder.Property(x => x.Accion).HasColumnName("accion").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Entidad).HasColumnName("entidad").HasMaxLength(50);
        builder.Property(x => x.EntidadId).HasColumnName("entidad_id").HasMaxLength(64);
        builder.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(45);
        builder.Property(x => x.Detalle).HasColumnName("detalle").HasColumnType("jsonb");
        builder.Property(x => x.Exito).HasColumnName("exito").IsRequired();

        // Sin FK a usuarios: el historial de auditoría no debe depender de que el usuario exista.
        builder.HasIndex(x => x.FechaUtc).HasDatabaseName("ix_auditoria_fecha_utc");
        builder.HasIndex(x => new { x.UsuarioId, x.FechaUtc }).HasDatabaseName("ix_auditoria_usuario_fecha");
        builder.HasIndex(x => x.Accion).HasDatabaseName("ix_auditoria_accion");
    }
}
