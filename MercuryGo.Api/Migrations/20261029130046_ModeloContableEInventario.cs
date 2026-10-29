using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercuryGo.Api.Migrations
{
    /// <inheritdoc />
    public partial class ModeloContableEInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "cliente_id",
                table: "usuarios",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "stock_actual",
                table: "productos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "stock_minimo",
                table: "productos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "pagos_cuenta_corriente",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cliente_id = table.Column<long>(type: "bigint", nullable: false),
                    monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    comprobante_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    estado = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false, defaultValue: "Pendiente")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    observaciones = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    motivo_rechazo = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    auditado_por_id = table.Column<long>(type: "bigint", nullable: true),
                    auditado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    activo = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pagos_cuenta_corriente", x => x.id);
                    table.ForeignKey(
                        name: "fk_pagos_cuenta_corriente_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pagos_cuenta_corriente_usuarios_auditado_por_id",
                        column: x => x.auditado_por_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 1L,
                columns: new[] { "stock_actual", "stock_minimo" },
                values: new object[] { 100, 10 });

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 2L,
                columns: new[] { "stock_actual", "stock_minimo" },
                values: new object[] { 100, 10 });

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 3L,
                columns: new[] { "stock_actual", "stock_minimo" },
                values: new object[] { 100, 10 });

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 4L,
                columns: new[] { "stock_actual", "stock_minimo" },
                values: new object[] { 100, 10 });

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 5L,
                columns: new[] { "stock_actual", "stock_minimo" },
                values: new object[] { 100, 10 });

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 6L,
                columns: new[] { "stock_actual", "stock_minimo" },
                values: new object[] { 100, 10 });

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: 1L,
                column: "cliente_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: 2L,
                column: "cliente_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: 3L,
                column: "cliente_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: 4L,
                column: "cliente_id",
                value: 1L);

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: 5L,
                column: "cliente_id",
                value: null);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_cliente_id",
                table: "usuarios",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagos_cuenta_corriente_auditado_por_id",
                table: "pagos_cuenta_corriente",
                column: "auditado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagos_cuenta_corriente_cliente_id",
                table: "pagos_cuenta_corriente",
                column: "cliente_id");

            migrationBuilder.AddForeignKey(
                name: "fk_usuarios_clientes_cliente_id",
                table: "usuarios",
                column: "cliente_id",
                principalTable: "clientes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_usuarios_clientes_cliente_id",
                table: "usuarios");

            migrationBuilder.DropTable(
                name: "pagos_cuenta_corriente");

            migrationBuilder.DropIndex(
                name: "ix_usuarios_cliente_id",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "cliente_id",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "stock_actual",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "stock_minimo",
                table: "productos");
        }
    }
}
