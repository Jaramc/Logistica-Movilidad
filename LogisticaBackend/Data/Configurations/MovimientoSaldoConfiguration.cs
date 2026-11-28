using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LogisticaBackend.Domain.Entities;

namespace LogisticaBackend.Data.Configurations;

public class MovimientoSaldoConfiguration : IEntityTypeConfiguration<MovimientoSaldo>
{
    public void Configure(EntityTypeBuilder<MovimientoSaldo> builder)
    {
        builder.ToTable("movimientos_saldo", t =>
        {
            // Inicial y Ajuste son correcciones o puntos de partida: siempre deben explicarse.
            t.HasCheckConstraint(RestriccionesCatalogos.MovimientoJustificacionObligatoria,
                "tipo NOT IN ('Inicial', 'Ajuste') OR justificacion IS NOT NULL");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();

        builder.Property(x => x.ConductorId).HasColumnName("conductor_id").IsRequired();
        builder.Property(x => x.ViajeId).HasColumnName("viaje_id");
        builder.Property(x => x.PagoSaldoId).HasColumnName("pago_saldo_id");

        builder.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Valor).HasColumnName("valor").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.SaldoResultante).HasColumnName("saldo_resultante").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.FechaCorte).HasColumnName("fecha_corte").HasColumnType("date").IsRequired();
        builder.Property(x => x.Justificacion).HasColumnName("justificacion").HasMaxLength(300);
        builder.Property(x => x.CreadoPorUsuarioId).HasColumnName("creado_por_usuario_id");

        builder.Property(x => x.CreadoEn)
            .HasColumnName("creado_en")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(x => x.Conductor).WithMany().HasForeignKey(x => x.ConductorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Viaje).WithMany().HasForeignKey(x => x.ViajeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PagoSaldo).WithMany().HasForeignKey(x => x.PagoSaldoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Usuario>().WithMany().HasForeignKey(x => x.CreadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ConductorId).HasDatabaseName("ix_movimientos_saldo_conductor_id");
        builder.HasIndex(x => x.ViajeId).HasDatabaseName("ix_movimientos_saldo_viaje_id");
        builder.HasIndex(x => x.PagoSaldoId).HasDatabaseName("ix_movimientos_saldo_pago_saldo_id");
    }
}
