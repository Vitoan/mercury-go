using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercuryGo.Api.Migrations
{
    /// <inheritdoc />
    public partial class RecorridoDefensaDemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Asegurar stock abundante en productos clave del catálogo para múltiples ensayos de demo
            migrationBuilder.Sql(@"
                UPDATE productos
                SET stock_actual = 150, disponible = 1, activo = 1
                WHERE id IN (1, 2, 4, 5, 6);
            ");

            // 2. Asignar el pedido PED-000002 (Id = 2) al chofer demo (Usuario Id = 3) en estado Despachado
            // para demostrar entrega con GPS (EnPuerta -> Entregado) y cobro sin demoras.
            migrationBuilder.Sql(@"
                UPDATE pedidos
                SET chofer_id = 3,
                    estado = 'Despachado',
                    activo = 1
                WHERE id = 2;
            ");

            // 3. Dejar el pedido PED-000003 (Id = 3, ClienteId = 1) en estado Confirmado
            // para que el Operario lo prepare, despache y emita el Remito PDF con QR.
            migrationBuilder.Sql(@"
                UPDATE pedidos
                SET estado = 'Confirmado',
                    chofer_id = NULL,
                    activo = 1
                WHERE id = 3;
            ");

            // 4. Dejar el pedido PED-000005 (Id = 5) asignado al ClienteId = 1 (cliente@mercurygo.local)
            // en estado Pendiente para pruebas de edición (ADMIN) o solicitud de cancelación (CLIENTE).
            migrationBuilder.Sql(@"
                UPDATE pedidos
                SET cliente_id = 1,
                    estado = 'Pendiente',
                    chofer_id = NULL,
                    activo = 1
                WHERE id = 5;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE pedidos SET chofer_id = NULL WHERE id = 2;
                UPDATE pedidos SET estado = 'EnPreparacion' WHERE id = 3;
                UPDATE pedidos SET cliente_id = 2, estado = 'Pendiente' WHERE id = 5;
            ");
        }
    }
}
