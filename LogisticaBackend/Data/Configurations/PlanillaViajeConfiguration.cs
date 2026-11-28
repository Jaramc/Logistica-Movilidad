using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LogisticaBackend.Domain.Entities;

namespace LogisticaBackend.Data.Configurations;

public class PlanillaViajeConfiguration : IEntityTypeConfiguration<PlanillaViaje>
{
    public void Configure(EntityTypeBuilder<PlanillaViaje> builder)
    {
        builder.ToTable("planillas_viaje", t =>
        {
            // Defensa en la base: aunque alguien salte la API, no entra basura. La migración
            // RestriccionesPlanilla las crea NOT VALID y solo las valida si los datos existentes cumplen.
            t.HasCheckConstraint(RestriccionesPlanilla.EmpresaConLetras, "empresa_transportadora IS NULL OR empresa_transportadora ~ '[[:alpha:]].*[[:alpha:]]'");
            t.HasCheckConstraint(RestriccionesPlanilla.PlacaFormato, "placa ~ '^[A-Z]{3}[0-9]{3}$'");
            t.HasCheckConstraint(RestriccionesPlanilla.FechasEnOrden, "fecha_final >= fecha_inicial");
        });

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

        builder.Property(x => x.ViajeId).HasColumnName("viaje_id");
        builder.HasOne(x => x.Viaje).WithOne(v => v.Planilla).HasForeignKey<PlanillaViaje>(x => x.ViajeId).OnDelete(DeleteBehavior.Restrict);
        // NULL no cuenta como duplicado en PostgreSQL: las planillas sin viaje (anteriores a la fase 1)
        // no chocan entre sí, pero un viaje no puede tener dos planillas.
        builder.HasIndex(x => x.ViajeId).IsUnique().HasDatabaseName(RestriccionesCatalogos.PlanillaViajeUnica);

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

        builder.Property(x => x.TotalGastosDeclarado).HasColumnName("total_gastos_declarado").HasPrecision(14, 2);
        builder.Property(x => x.SaldoViajeDeclarado).HasColumnName("saldo_viaje_declarado").HasPrecision(14, 2);
        builder.Property(x => x.SaldoAnteriorDeclarado).HasColumnName("saldo_anterior_declarado").HasPrecision(14, 2);
        builder.Property(x => x.SaldoNuevoDeclarado).HasColumnName("saldo_nuevo_declarado").HasPrecision(14, 2);
        builder.Property(x => x.TieneDiferencias).HasColumnName("tiene_diferencias").HasDefaultValue(false).IsRequired();

        builder.Property(x => x.EstadoRevision)
            .HasColumnName("estado_revision")
            .HasConversion<string>()
            .HasMaxLength(20)
            // Sin valor por defecto en la base: el servidor SIEMPRE asigna el estado. Con un default
            // 'Aprobado', una planilla en 'Pendiente' (valor CLR por defecto) quedaría aprobada en silencio.
            .IsRequired();

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

/// <summary>Nombres de los CHECK de la planilla (los usa la migración RestriccionesPlanilla).</summary>
public static class RestriccionesPlanilla
{
    public const string EmpresaConLetras = "ck_planillas_viaje_empresa_con_letras";
    public const string PlacaFormato = "ck_planillas_viaje_placa_formato";
    public const string FechasEnOrden = "ck_planillas_viaje_fechas_en_orden";
    public const string ValorNoNegativo = "ck_gastos_detalle_valor_no_negativo";
    public const string CategoriaValida = "ck_gastos_detalle_categoria_valida";

    /// <summary>Las 16 categorías como lista SQL: 'PEAJES', 'PORCENTAJE', ...</summary>
    public static string CategoriasSql =>
        string.Join(", ", LogisticaBackend.Domain.Services.ConceptoNormalizer.CategoriasCanonicas.Select(c => $"'{c}'"));
}
