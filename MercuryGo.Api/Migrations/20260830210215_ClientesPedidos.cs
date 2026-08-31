using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MercuryGo.Api.Migrations
{
    /// <inheritdoc />
    public partial class ClientesPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    razon_social = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cuit = table.Column<string>(type: "varchar(13)", maxLength: 13, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    telefono = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    direccion = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    localidad = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    activo = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clientes", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pedidos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cliente_id = table.Column<long>(type: "bigint", nullable: false),
                    numero = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    estado = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_pedido = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    observaciones = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    activo = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedidos", x => x.id);
                    table.ForeignKey(
                        name: "fk_pedidos_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "detalles_pedido",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    pedido_id = table.Column<long>(type: "bigint", nullable: false),
                    producto_id = table.Column<long>(type: "bigint", nullable: false),
                    cantidad = table.Column<int>(type: "int", nullable: false),
                    precio_unitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_detalles_pedido", x => x.id);
                    table.ForeignKey(
                        name: "fk_detalles_pedido_pedidos_pedido_id",
                        column: x => x.pedido_id,
                        principalTable: "pedidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_detalles_pedido_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "clientes",
                columns: new[] { "id", "activo", "actualizado_en", "creado_en", "cuit", "direccion", "email", "localidad", "razon_social", "telefono" },
                values: new object[,]
                {
                    { 1L, true, null, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "30-71234567-0", "Av. San Martín 1520", "compras@elnorte.com.ar", "San Luis", "Distribuidora El Norte S.R.L.", "266-4123456" },
                    { 2L, true, null, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "30-68901234-1", "Belgrano 890", "pedidos@lafamilia.com.ar", "Villa Mercedes", "Supermercado La Familia S.A.", "266-4234567" },
                    { 3L, true, null, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "20-30123456-9", "Mitre 345", null, "San Luis", "Almacenes Pérez Hnos.", "266-4345678" }
                });

            migrationBuilder.InsertData(
                table: "clientes",
                columns: new[] { "id", "actualizado_en", "creado_en", "cuit", "direccion", "email", "localidad", "razon_social", "telefono" },
                values: new object[] { 4L, null, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "27-40234567-2", "Colón 1200", "lospinos@gmail.com", "Merlo", "Minimarket Los Pinos", "266-4456789" });

            migrationBuilder.InsertData(
                table: "pedidos",
                columns: new[] { "id", "activo", "actualizado_en", "cliente_id", "creado_en", "estado", "fecha_pedido", "numero", "observaciones" },
                values: new object[,]
                {
                    { 1L, true, null, 1L, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Entregado", new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "PED-000001", "Entregar en depósito trasero" },
                    { 2L, true, null, 2L, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Despachado", new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "PED-000002", null },
                    { 3L, true, null, 1L, new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), "EnPreparacion", new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), "PED-000003", "Urgente, requiere factura A" },
                    { 4L, true, null, 3L, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Confirmado", new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), "PED-000004", null },
                    { 5L, true, null, 2L, new DateTime(2026, 8, 30, 0, 0, 0, 0, DateTimeKind.Utc), "Pendiente", new DateTime(2026, 8, 30, 0, 0, 0, 0, DateTimeKind.Utc), "PED-000005", null }
                });

            migrationBuilder.InsertData(
                table: "detalles_pedido",
                columns: new[] { "id", "actualizado_en", "cantidad", "creado_en", "pedido_id", "precio_unitario", "producto_id" },
                values: new object[,]
                {
                    { 1L, null, 5, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1L, 2400.00m, 1L },
                    { 2L, null, 10, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1L, 1250.00m, 5L },
                    { 3L, null, 3, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1L, 3400.00m, 6L },
                    { 4L, null, 20, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), 2L, 950.00m, 2L },
                    { 5L, null, 15, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), 2L, 1100.00m, 4L },
                    { 6L, null, 8, new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), 3L, 2400.00m, 1L },
                    { 7L, null, 6, new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), 3L, 3400.00m, 6L },
                    { 8L, null, 12, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), 4L, 1250.00m, 5L },
                    { 9L, null, 4, new DateTime(2026, 8, 30, 0, 0, 0, 0, DateTimeKind.Utc), 5L, 1800.00m, 3L },
                    { 10L, null, 8, new DateTime(2026, 8, 30, 0, 0, 0, 0, DateTimeKind.Utc), 5L, 950.00m, 2L }
                });

            migrationBuilder.CreateIndex(
                name: "ix_clientes_cuit",
                table: "clientes",
                column: "cuit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_detalles_pedido_pedido_id",
                table: "detalles_pedido",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ix_detalles_pedido_producto_id",
                table: "detalles_pedido",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_cliente_id",
                table: "pedidos",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_numero",
                table: "pedidos",
                column: "numero",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalles_pedido");

            migrationBuilder.DropTable(
                name: "pedidos");

            migrationBuilder.DropTable(
                name: "clientes");
        }
    }
}
