using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogisticaBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class InmutabilidadMovimientosSaldo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION movimientos_saldo_solo_insercion() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'La tabla movimientos_saldo es de solo inserción (operación % rechazada).', TG_OP;
END;
$$;

CREATE TRIGGER trg_movimientos_saldo_sin_update_delete
    BEFORE UPDATE OR DELETE ON movimientos_saldo
    FOR EACH ROW EXECUTE FUNCTION movimientos_saldo_solo_insercion();

CREATE TRIGGER trg_movimientos_saldo_sin_truncate
    BEFORE TRUNCATE ON movimientos_saldo
    FOR EACH STATEMENT EXECUTE FUNCTION movimientos_saldo_solo_insercion();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_movimientos_saldo_sin_truncate ON movimientos_saldo;
DROP TRIGGER IF EXISTS trg_movimientos_saldo_sin_update_delete ON movimientos_saldo;
DROP FUNCTION IF EXISTS movimientos_saldo_solo_insercion();
");
        }
    }
}
