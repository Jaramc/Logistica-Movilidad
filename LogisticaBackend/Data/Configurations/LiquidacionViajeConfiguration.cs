using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LogisticaBackend.Models;

namespace LogisticaBackend.Data.Configurations;

public class LiquidacionViajeConfiguration : IEntityTypeConfiguration<LiquidacionViaje>
{
    public void Configure(EntityTypeBuilder<LiquidacionViaje> builder)
    {
        // Nombre de la tabla
        builder.ToTable("liquidaciones_viaje");

        // Llave primaria
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();

        // Información General
        builder.Property(x => x.FechaInicial)
            .HasColumnName("fecha_inicial")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.FechaFinal)
            .HasColumnName("fecha_final")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.Conductor)
            .HasColumnName("conductor")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Placa)
            .HasColumnName("placa")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Manifiesto)
            .HasColumnName("manifiesto")
            .HasMaxLength(50);

        builder.Property(x => x.EmpresaTransportadora)
            .HasColumnName("empresa_transportadora")
            .HasMaxLength(150);

        builder.Property(x => x.OrigenViaje)
            .HasColumnName("origen_viaje")
            .HasMaxLength(100);

        builder.Property(x => x.Destino)
            .HasColumnName("destino")
            .HasMaxLength(100);

        // Gastos Operativos (NUMERIC(12, 2) DEFAULT 0)
        ConfigureDecimal(builder.Property(x => x.Peajes), "peajes");
        ConfigureDecimal(builder.Property(x => x.Porcentaje), "porcentaje");
        ConfigureDecimal(builder.Property(x => x.Comision), "comision");
        ConfigureDecimal(builder.Property(x => x.Parqueo), "parqueo");
        ConfigureDecimal(builder.Property(x => x.Acpm), "acpm");
        ConfigureDecimal(builder.Property(x => x.Engrase), "engrase");
        ConfigureDecimal(builder.Property(x => x.Tensionada), "tensionada");
        ConfigureDecimal(builder.Property(x => x.Aseo), "aseo");
        ConfigureDecimal(builder.Property(x => x.Llantas), "llantas");
        ConfigureDecimal(builder.Property(x => x.Urea), "urea");
        ConfigureDecimal(builder.Property(x => x.Cargue), "cargue");
        ConfigureDecimal(builder.Property(x => x.Descargue), "descargue");
        ConfigureDecimal(builder.Property(x => x.Carrosada), "carrosada");
        ConfigureDecimal(builder.Property(x => x.Descarrosada), "descarrosada");
        ConfigureDecimal(builder.Property(x => x.Papeleria), "papeleria");
        ConfigureDecimal(builder.Property(x => x.Otros), "otros");

        // Totales y Saldos (NUMERIC(12, 2) DEFAULT 0)
        ConfigureDecimal(builder.Property(x => x.TotalGastos), "total_gastos");
        ConfigureDecimal(builder.Property(x => x.Traslado), "traslado");
        ConfigureDecimal(builder.Property(x => x.Basico), "basico");
        ConfigureDecimal(builder.Property(x => x.SaldoViaje), "saldo_viaje");
        ConfigureDecimal(builder.Property(x => x.SaldoAnterior), "saldo_anterior");
        ConfigureDecimal(builder.Property(x => x.SaldoTotal), "saldo_total");

        // Auditoría y Control
        builder.Property(x => x.EstadoRevision)
            .HasColumnName("estado_revision")
            .HasMaxLength(20)
            .HasDefaultValue("pendiente");

        // Mapeo JSONB para Npgsql
        builder.Property(x => x.JsonOcrCrudo)
            .HasColumnName("json_ocr_crudo")
            .HasColumnType("jsonb");

        builder.Property(x => x.CreadoEn)
            .HasColumnName("creado_en")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.ActualizadoEn)
            .HasColumnName("actualizado_en")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }

    private static void ConfigureDecimal(PropertyBuilder<decimal> propertyBuilder, string columnName)
    {
        propertyBuilder
            .HasColumnName(columnName)
            .HasPrecision(12, 2)
            .HasDefaultValue(0m);
    }
}
