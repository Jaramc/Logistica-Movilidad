using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LogisticaBackend.Domain.Entities;

namespace LogisticaBackend.Data.Configurations;

public class PlanillaViajeConfiguration : IEntityTypeConfiguration<PlanillaViaje>
{
    public void Configure(EntityTypeBuilder<PlanillaViaje> builder)
    {
        builder.ToTable("planillas_viaje");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();

        builder.Property(x => x.FechaInicial)
            .HasColumnName("fecha_inicial")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.FechaFinal)
            .HasColumnName("fecha_final")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.Placa)
            .HasColumnName("placa")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Conductor)
            .HasColumnName("conductor")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Manifiesto)
            .HasColumnName("manifiesto")
            .HasMaxLength(50);

        builder.Property(x => x.EmpresaTransportadora)
            .HasColumnName("empresa_transportadora")
            .HasMaxLength(150);

        builder.Property(x => x.Origen)
            .HasColumnName("origen")
            .HasMaxLength(100);

        builder.Property(x => x.Destino)
            .HasColumnName("destino")
            .HasMaxLength(100);

        // Campos Financieros Reales NUMERIC(14,2)
        ConfigureDecimal(builder.Property(x => x.Flete), "flete");
        ConfigureDecimal(builder.Property(x => x.Anticipo), "anticipo");
        ConfigureDecimal(builder.Property(x => x.TotalGastos), "total_gastos");
        ConfigureDecimal(builder.Property(x => x.SaldoViaje), "saldo_viaje");
        ConfigureDecimal(builder.Property(x => x.SaldoAnterior), "saldo_anterior");
        ConfigureDecimal(builder.Property(x => x.SaldoTotal), "saldo_total");

        builder.Property(x => x.EstadoRevision)
            .HasColumnName("estado_revision")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(EstadoRevision.Aprobado);

        builder.Property(x => x.JsonOcrCrudo)
            .HasColumnName("json_ocr_crudo")
            .HasColumnType("jsonb");

        builder.Property(x => x.CreadoPorUsuarioId).HasColumnName("creado_por_usuario_id");
        builder.Property(x => x.RevisadoPorUsuarioId).HasColumnName("revisado_por_usuario_id");
        builder.HasOne(x => x.CreadoPor).WithMany().HasForeignKey(x => x.CreadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RevisadoPor).WithMany().HasForeignKey(x => x.RevisadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CreadoEn)
            .HasColumnName("creado_en")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.ActualizadoEn)
            .HasColumnName("actualizado_en")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Concurrencia Optimista
        builder.Property(x => x.Version).IsRowVersion();

        // Índices para optimizar búsquedas frecuentes
        builder.HasIndex(x => x.Placa)
            .HasDatabaseName("ix_planillas_viaje_placa");

        builder.HasIndex(x => x.Manifiesto)
            .HasDatabaseName("ix_planillas_viaje_manifiesto");

        builder.HasIndex(x => x.FechaInicial)
            .HasDatabaseName("ix_planillas_viaje_fecha_inicial");

        // Relación 1:N en Cascada
        builder.HasMany(x => x.Gastos)
            .WithOne(g => g.PlanillaViaje)
            .HasForeignKey(g => g.PlanillaViajeId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureDecimal(PropertyBuilder<decimal> propertyBuilder, string columnName)
    {
        propertyBuilder
            .HasColumnName(columnName)
            .HasPrecision(14, 2)
            .HasDefaultValue(0m);
    }
}
