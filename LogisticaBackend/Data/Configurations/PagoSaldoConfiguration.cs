using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LogisticaBackend.Domain.Entities;

namespace LogisticaBackend.Data.Configurations;

public class PagoSaldoConfiguration : IEntityTypeConfiguration<PagoSaldo>
{
    public void Configure(EntityTypeBuilder<PagoSaldo> builder)
    {
        builder.ToTable("pagos_saldo", t =>
        {
            t.HasCheckConstraint(RestriccionesCatalogos.PagoValorPositivo, "valor > 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();

        builder.Property(x => x.ConductorId).HasColumnName("conductor_id").IsRequired();
        builder.Property(x => x.ViajeId).HasColumnName("viaje_id");

        builder.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Valor).HasColumnName("valor").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.Fecha).HasColumnName("fecha").HasColumnType("date").IsRequired();
        builder.Property(x => x.Quincena).HasColumnName("quincena").HasMaxLength(20);
        builder.Property(x => x.Medio).HasColumnName("medio").HasMaxLength(30);
        builder.Property(x => x.Nota).HasColumnName("nota").HasMaxLength(200);
        builder.Property(x => x.RegistradoPorUsuarioId).HasColumnName("registrado_por_usuario_id");

        builder.Property(x => x.CreadoEn)
            .HasColumnName("creado_en")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(x => x.Conductor).WithMany().HasForeignKey(x => x.ConductorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Viaje).WithMany().HasForeignKey(x => x.ViajeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Usuario>().WithMany().HasForeignKey(x => x.RegistradoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ConductorId).HasDatabaseName("ix_pagos_saldo_conductor_id");
        builder.HasIndex(x => x.ViajeId).HasDatabaseName("ix_pagos_saldo_viaje_id");
    }
}
