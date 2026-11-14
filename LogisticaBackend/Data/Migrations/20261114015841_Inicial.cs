using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LogisticaBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fecha_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entidad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entidad_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    detalle = table.Column<string>(type: "jsonb", nullable: true),
                    exito = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auditoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreCompleto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DebeCambiarPassword = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UltimoAcceso = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordTemporalExpiraEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordTemporalUsadaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_roles_claims_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planillas_viaje",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fecha_inicial = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_final = table.Column<DateOnly>(type: "date", nullable: false),
                    placa = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    conductor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    manifiesto = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    empresa_transportadora = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    origen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    destino = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    flete = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    anticipo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    total_gastos = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    saldo_viaje = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    saldo_anterior = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    saldo_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    estado_revision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    json_ocr_crudo = table.Column<string>(type: "jsonb", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revisado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planillas_viaje", x => x.id);
                    table.ForeignKey(
                        name: "FK_planillas_viaje_usuarios_creado_por_usuario_id",
                        column: x => x.creado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_planillas_viaje_usuarios_revisado_por_usuario_id",
                        column: x => x.revisado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_usuarios_claims_usuarios_UserId",
                        column: x => x.UserId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_logins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios_logins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_usuarios_logins_usuarios_UserId",
                        column: x => x.UserId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_usuarios_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_usuarios_roles_usuarios_UserId",
                        column: x => x.UserId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_tokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios_tokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_usuarios_tokens_usuarios_UserId",
                        column: x => x.UserId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gastos_detalle",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    planilla_viaje_id = table.Column<int>(type: "integer", nullable: false),
                    concepto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    concepto_canonico = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "OTROS"),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    orden = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gastos_detalle", x => x.id);
                    table.ForeignKey(
                        name: "FK_gastos_detalle_planillas_viaje_planilla_viaje_id",
                        column: x => x.planilla_viaje_id,
                        principalTable: "planillas_viaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_accion",
                table: "auditoria",
                column: "accion");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_fecha_utc",
                table: "auditoria",
                column: "fecha_utc");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_usuario_fecha",
                table: "auditoria",
                columns: new[] { "usuario_id", "fecha_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_gastos_detalle_concepto_canonico",
                table: "gastos_detalle",
                column: "concepto_canonico");

            migrationBuilder.CreateIndex(
                name: "ix_gastos_detalle_planilla_viaje_id",
                table: "gastos_detalle",
                column: "planilla_viaje_id");

            migrationBuilder.CreateIndex(
                name: "IX_planillas_viaje_creado_por_usuario_id",
                table: "planillas_viaje",
                column: "creado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_planillas_viaje_fecha_inicial",
                table: "planillas_viaje",
                column: "fecha_inicial");

            migrationBuilder.CreateIndex(
                name: "ix_planillas_viaje_manifiesto",
                table: "planillas_viaje",
                column: "manifiesto");

            migrationBuilder.CreateIndex(
                name: "ix_planillas_viaje_placa",
                table: "planillas_viaje",
                column: "placa");

            migrationBuilder.CreateIndex(
                name: "IX_planillas_viaje_revisado_por_usuario_id",
                table: "planillas_viaje",
                column: "revisado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_claims_RoleId",
                table: "roles_claims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "usuarios",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "usuarios",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_claims_UserId",
                table: "usuarios_claims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_logins_UserId",
                table: "usuarios_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_roles_RoleId",
                table: "usuarios_roles",
                column: "RoleId");

            // Auditoría de SOLO inserción: ni siquiera el dueño de la tabla puede modificarla
            // o borrarla sin desactivar antes el trigger (lo que queda en los logs de PostgreSQL).
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION auditoria_solo_insercion() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'La tabla auditoria es de solo inserción (operación % rechazada).', TG_OP;
END;
$$;

CREATE TRIGGER trg_auditoria_sin_update_delete
    BEFORE UPDATE OR DELETE ON auditoria
    FOR EACH ROW EXECUTE FUNCTION auditoria_solo_insercion();

CREATE TRIGGER trg_auditoria_sin_truncate
    BEFORE TRUNCATE ON auditoria
    FOR EACH STATEMENT EXECUTE FUNCTION auditoria_solo_insercion();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_auditoria_sin_truncate ON auditoria;
DROP TRIGGER IF EXISTS trg_auditoria_sin_update_delete ON auditoria;
DROP FUNCTION IF EXISTS auditoria_solo_insercion();
");

            migrationBuilder.DropTable(
                name: "auditoria");

            migrationBuilder.DropTable(
                name: "gastos_detalle");

            migrationBuilder.DropTable(
                name: "roles_claims");

            migrationBuilder.DropTable(
                name: "usuarios_claims");

            migrationBuilder.DropTable(
                name: "usuarios_logins");

            migrationBuilder.DropTable(
                name: "usuarios_roles");

            migrationBuilder.DropTable(
                name: "usuarios_tokens");

            migrationBuilder.DropTable(
                name: "planillas_viaje");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
