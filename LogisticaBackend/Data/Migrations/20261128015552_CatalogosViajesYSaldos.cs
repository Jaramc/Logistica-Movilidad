using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LogisticaBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class CatalogosViajesYSaldos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "seq_conductores_codigo");

            migrationBuilder.AddColumn<int>(
                name: "viaje_id",
                table: "planillas_viaje",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "conductores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nombre_completo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cedula = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    cuenta_microsoft = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conductores", x => x.id);
                    table.CheckConstraint("ck_conductores_cedula_formato", "cedula ~ '^[0-9]{6,12}$'");
                });

            migrationBuilder.CreateTable(
                name: "vehiculos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    placa = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Tractomula"),
                    categoria_peaje = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehiculos", x => x.id);
                    table.CheckConstraint("ck_vehiculos_placa_formato", "placa ~ '^[A-Z]{3}[0-9]{3}$'");
                });

            migrationBuilder.CreateTable(
                name: "viajes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conductor_id = table.Column<int>(type: "integer", nullable: false),
                    vehiculo_id = table.Column<int>(type: "integer", nullable: false),
                    origen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    destino = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cliente = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    manifiesto = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    flete = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    saldo_viaje = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aprobado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aprobado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_viajes", x => x.id);
                    table.CheckConstraint("ck_viajes_fechas_en_orden", "fecha_fin IS NULL OR fecha_fin >= fecha_inicio");
                    table.ForeignKey(
                        name: "FK_viajes_conductores_conductor_id",
                        column: x => x.conductor_id,
                        principalTable: "conductores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_viajes_usuarios_aprobado_por_usuario_id",
                        column: x => x.aprobado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_viajes_usuarios_creado_por_usuario_id",
                        column: x => x.creado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_viajes_vehiculos_vehiculo_id",
                        column: x => x.vehiculo_id,
                        principalTable: "vehiculos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pagos_saldo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conductor_id = table.Column<int>(type: "integer", nullable: false),
                    viaje_id = table.Column<int>(type: "integer", nullable: true),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    quincena = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    medio = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    nota = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    registrado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagos_saldo", x => x.id);
                    table.CheckConstraint("ck_pagos_saldo_valor_positivo", "valor > 0");
                    table.ForeignKey(
                        name: "FK_pagos_saldo_conductores_conductor_id",
                        column: x => x.conductor_id,
                        principalTable: "conductores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pagos_saldo_usuarios_registrado_por_usuario_id",
                        column: x => x.registrado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pagos_saldo_viajes_viaje_id",
                        column: x => x.viaje_id,
                        principalTable: "viajes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "traslados",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    viaje_id = table.Column<int>(type: "integer", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    medio = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nota = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    registrado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_traslados", x => x.id);
                    table.CheckConstraint("ck_traslados_valor_positivo", "valor > 0");
                    table.ForeignKey(
                        name: "FK_traslados_usuarios_registrado_por_usuario_id",
                        column: x => x.registrado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_traslados_viajes_viaje_id",
                        column: x => x.viaje_id,
                        principalTable: "viajes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "movimientos_saldo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conductor_id = table.Column<int>(type: "integer", nullable: false),
                    viaje_id = table.Column<int>(type: "integer", nullable: true),
                    pago_saldo_id = table.Column<int>(type: "integer", nullable: true),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    saldo_resultante = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    fecha_corte = table.Column<DateOnly>(type: "date", nullable: false),
                    justificacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimientos_saldo", x => x.id);
                    table.CheckConstraint("ck_movimientos_saldo_justificacion_obligatoria", "tipo NOT IN ('Inicial', 'Ajuste') OR justificacion IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_movimientos_saldo_conductores_conductor_id",
                        column: x => x.conductor_id,
                        principalTable: "conductores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_saldo_pagos_saldo_pago_saldo_id",
                        column: x => x.pago_saldo_id,
                        principalTable: "pagos_saldo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_saldo_usuarios_creado_por_usuario_id",
                        column: x => x.creado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_saldo_viajes_viaje_id",
                        column: x => x.viaje_id,
                        principalTable: "viajes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "vehiculos",
                columns: new[] { "id", "activo", "categoria_peaje", "placa", "tipo" },
                values: new object[,]
                {
                    { 1, true, null, "JYP108", "Tractomula" },
                    { 2, true, null, "JYP052", "Tractomula" },
                    { 3, true, null, "NNN977", "Tractomula" },
                    { 4, true, null, "STW514", "Tractomula" }
                });

            migrationBuilder.CreateIndex(
                name: "ux_planillas_viaje_viaje_id",
                table: "planillas_viaje",
                column: "viaje_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_conductores_cedula",
                table: "conductores",
                column: "cedula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_conductores_codigo",
                table: "conductores",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_saldo_conductor_id",
                table: "movimientos_saldo",
                column: "conductor_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_saldo_creado_por_usuario_id",
                table: "movimientos_saldo",
                column: "creado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_saldo_pago_saldo_id",
                table: "movimientos_saldo",
                column: "pago_saldo_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_saldo_viaje_id",
                table: "movimientos_saldo",
                column: "viaje_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagos_saldo_conductor_id",
                table: "pagos_saldo",
                column: "conductor_id");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_saldo_registrado_por_usuario_id",
                table: "pagos_saldo",
                column: "registrado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagos_saldo_viaje_id",
                table: "pagos_saldo",
                column: "viaje_id");

            migrationBuilder.CreateIndex(
                name: "IX_traslados_registrado_por_usuario_id",
                table: "traslados",
                column: "registrado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_traslados_viaje_id",
                table: "traslados",
                column: "viaje_id");

            migrationBuilder.CreateIndex(
                name: "ux_vehiculos_placa",
                table: "vehiculos",
                column: "placa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_viajes_aprobado_por_usuario_id",
                table: "viajes",
                column: "aprobado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_viajes_creado_por_usuario_id",
                table: "viajes",
                column: "creado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ux_viajes_conductor_abierto",
                table: "viajes",
                column: "conductor_id",
                unique: true,
                filter: "estado IN ('Abierto', 'EnRevision')");

            migrationBuilder.CreateIndex(
                name: "ux_viajes_vehiculo_abierto",
                table: "viajes",
                column: "vehiculo_id",
                unique: true,
                filter: "estado IN ('Abierto', 'EnRevision')");

            migrationBuilder.AddForeignKey(
                name: "FK_planillas_viaje_viajes_viaje_id",
                table: "planillas_viaje",
                column: "viaje_id",
                principalTable: "viajes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_planillas_viaje_viajes_viaje_id",
                table: "planillas_viaje");

            migrationBuilder.DropTable(
                name: "movimientos_saldo");

            migrationBuilder.DropTable(
                name: "traslados");

            migrationBuilder.DropTable(
                name: "pagos_saldo");

            migrationBuilder.DropTable(
                name: "viajes");

            migrationBuilder.DropTable(
                name: "conductores");

            migrationBuilder.DropTable(
                name: "vehiculos");

            migrationBuilder.DropIndex(
                name: "ux_planillas_viaje_viaje_id",
                table: "planillas_viaje");

            migrationBuilder.DropColumn(
                name: "viaje_id",
                table: "planillas_viaje");

            migrationBuilder.DropSequence(
                name: "seq_conductores_codigo");
        }
    }
}
