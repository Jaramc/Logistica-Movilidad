using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogisticaBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class RestriccionesPlanilla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "saldo_anterior_declarado",
                table: "planillas_viaje",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "saldo_nuevo_declarado",
                table: "planillas_viaje",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "saldo_viaje_declarado",
                table: "planillas_viaje",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "tiene_diferencias",
                table: "planillas_viaje",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "total_gastos_declarado",
                table: "planillas_viaje",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "gastos_detalle",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "etiqueta",
                table: "gastos_detalle",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            // 1. Placas existentes: mayúsculas, sin espacios ni guiones ("wcn-410" → "WCN410").
            migrationBuilder.Sql(@"
UPDATE planillas_viaje
SET placa = upper(regexp_replace(placa, '[\s-]', '', 'g'))
WHERE placa <> upper(regexp_replace(placa, '[\s-]', '', 'g'));");

            // 2. Las restricciones se crean NOT VALID: rigen desde ya para filas nuevas o modificadas,
            //    pero no rechazan datos históricos. Cada una se valida solo si todas las filas cumplen;
            //    si no, se listan en el log de la migración (WARNING) y no se borra nada.
            foreach (var (tabla, nombre, condicion) in Restricciones)
            {
                migrationBuilder.Sql($@"ALTER TABLE {tabla} ADD CONSTRAINT {nombre} CHECK ({condicion}) NOT VALID;");
                migrationBuilder.Sql($@"
DO $$
DECLARE
    incumplen text;
BEGIN
    SELECT string_agg(id::text, ', ' ORDER BY id) INTO incumplen FROM {tabla} WHERE NOT ({condicion});
    IF incumplen IS NULL THEN
        ALTER TABLE {tabla} VALIDATE CONSTRAINT {nombre};
    ELSE
        RAISE WARNING '{nombre}: filas de {tabla} que no cumplen (no se borraron, corríjalas y ejecute VALIDATE CONSTRAINT): %', incumplen;
    END IF;
END $$;");
            }
        }

        /// <summary>Mismas condiciones que declaran PlanillaViajeConfiguration y GastoDetalleConfiguration.</summary>
        private static readonly (string Tabla, string Nombre, string Condicion)[] Restricciones =
        {
            ("planillas_viaje", "ck_planillas_viaje_empresa_con_letras", "empresa_transportadora IS NULL OR empresa_transportadora ~ '[[:alpha:]].*[[:alpha:]]'"),
            ("planillas_viaje", "ck_planillas_viaje_placa_formato", "placa ~ '^[A-Z]{3}[0-9]{3}$'"),
            ("planillas_viaje", "ck_planillas_viaje_fechas_en_orden", "fecha_final >= fecha_inicial"),
            ("gastos_detalle", "ck_gastos_detalle_valor_no_negativo", "valor >= 0"),
            ("gastos_detalle", "ck_gastos_detalle_categoria_valida", "concepto_canonico IN ('PEAJES', 'PORCENTAJE', 'COMISION', 'PARQUEO', 'ACPM', 'ENGRASE', 'TENSIONADA', 'ASEO', 'LLANTAS', 'UREA', 'CARGUE', 'DESCARGUE', 'CARROSADA', 'DESCARROSADA', 'PAPELERIA', 'OTROS')"),
        };

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_planillas_viaje_empresa_con_letras",
                table: "planillas_viaje");

            migrationBuilder.DropCheckConstraint(
                name: "ck_planillas_viaje_fechas_en_orden",
                table: "planillas_viaje");

            migrationBuilder.DropCheckConstraint(
                name: "ck_planillas_viaje_placa_formato",
                table: "planillas_viaje");

            migrationBuilder.DropCheckConstraint(
                name: "ck_gastos_detalle_categoria_valida",
                table: "gastos_detalle");

            migrationBuilder.DropCheckConstraint(
                name: "ck_gastos_detalle_valor_no_negativo",
                table: "gastos_detalle");

            migrationBuilder.DropColumn(
                name: "saldo_anterior_declarado",
                table: "planillas_viaje");

            migrationBuilder.DropColumn(
                name: "saldo_nuevo_declarado",
                table: "planillas_viaje");

            migrationBuilder.DropColumn(
                name: "saldo_viaje_declarado",
                table: "planillas_viaje");

            migrationBuilder.DropColumn(
                name: "tiene_diferencias",
                table: "planillas_viaje");

            migrationBuilder.DropColumn(
                name: "total_gastos_declarado",
                table: "planillas_viaje");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "gastos_detalle");

            migrationBuilder.DropColumn(
                name: "etiqueta",
                table: "gastos_detalle");
        }
    }
}
