using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercuryGo.Api.Migrations
{
    /// <inheritdoc />
    public partial class ModeloFase1Refactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "auditado_en",
                table: "pedidos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "auditado_por_id",
                table: "pedidos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancelacion_estado",
                table: "pedidos",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true,
                defaultValue: "Ninguna")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "cancelacion_motivo",
                table: "pedidos",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "cancelacion_solicitada_por_id",
                table: "pedidos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "chofer_id",
                table: "pedidos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "comprobante_estado",
                table: "pedidos",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true,
                defaultValue: "SinComprobante")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "comprobante_url",
                table: "pedidos",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "confirmado_en",
                table: "pedidos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "confirmado_por_id",
                table: "pedidos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_firma_url",
                table: "pedidos",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "entrega_foto_url",
                table: "pedidos",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "entregado_en",
                table: "pedidos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "picking_en",
                table: "pedidos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "picking_resultado",
                table: "pedidos",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "validacion_detalle",
                table: "pedidos",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "validacion_fallida",
                table: "pedidos",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "cantidad_entregada",
                table: "detalles_pedido",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cantidad_pedida",
                table: "detalles_pedido",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "cantidad_preparada",
                table: "detalles_pedido",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "cantidad_rechazada",
                table: "detalles_pedido",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_rechazo",
                table: "detalles_pedido",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cobros_pedido",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    pedido_id = table.Column<long>(type: "bigint", nullable: false),
                    monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    metodo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "Efectivo")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    registrado_por_id = table.Column<long>(type: "bigint", nullable: true),
                    ocurrido_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    recibido_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cobros_pedido", x => x.id);
                    table.ForeignKey(
                        name: "fk_cobros_pedido_pedidos_pedido_id",
                        column: x => x.pedido_id,
                        principalTable: "pedidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cobros_pedido_usuarios_registrado_por_id",
                        column: x => x.registrado_por_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "intentos_entrega",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    pedido_id = table.Column<long>(type: "bigint", nullable: false),
                    chofer_id = table.Column<long>(type: "bigint", nullable: true),
                    motivo = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    nota = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    foto_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ocurrido_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_intentos_entrega", x => x.id);
                    table.ForeignKey(
                        name: "fk_intentos_entrega_pedidos_pedido_id",
                        column: x => x.pedido_id,
                        principalTable: "pedidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_intentos_entrega_usuarios_chofer_id",
                        column: x => x.chofer_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "operaciones_procesadas",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    usuario_id = table.Column<long>(type: "bigint", nullable: true),
                    tipo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    pedido_id = table.Column<long>(type: "bigint", nullable: true),
                    resultado_json = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    procesada_en = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operaciones_procesadas", x => x.id);
                    table.ForeignKey(
                        name: "fk_operaciones_procesadas_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "detalles_pedido",
                keyColumn: "id",
                keyValue: 1L,
                columns: new[] { "cantidad_entregada", "cantidad_pedida", "cantidad_preparada", "cantidad_rechazada", "motivo_rechazo" },
                values: new object[] { null, 5, 5, null, null });

            migrationBuilder.UpdateData(
                table: "detalles_pedido",
                keyColumn: "id",
                keyValue: 2L,
                columns: new[] { "cantidad_entregada", "cantidad_pedida", "cantidad_preparada", "cantidad_rechazada", "motivo_rechazo" },
                values: new object[] { null, 10, 10, null, null });

            migrationBuilder.UpdateData(
                table: "detalles_pedido",
                keyColumn: "id",
                keyValue: 3L,
                columns: new[] { "cantidad_entregada", "cantidad_pedida", "cantidad_preparada", "cantidad_rechazada", "motivo_rechazo" },
                values: new object[] { null, 3, 3, null, null });

            migrationBuilder.UpdateData(
                table: "detalles_pedido",
                keyColumn: "id",
                keyValue: 4L,
                columns: new[] { "cantidad_entregada", "cantidad_pedida", "cantidad_preparada", "cantidad_rechazada", "motivo_rechazo" },
                values: new object[] { null, 20, 20, null, null });

            migrationBuilder.UpdateData(
                table: "detalles_pedido",
                keyColumn: "id",
                keyValue: 5L,
                columns: new[] { "cantidad_entregada", "cantidad_pedida", "cantidad_preparada", "cantidad_rechazada", "motivo_rechazo" },
                values: new object[] { null, 15, 15, null, null });

            migrationBuilder.UpdateData(
                table: "detalles_pedido",
                keyColumn: "id",
                keyValue: 6L,
                columns: new[] { "cantidad_entregada", "cantidad_pedida", "cantidad_preparada", "cantidad_rechazada", "motivo_rechazo" },
                values: new object[] { null, 8, 8, null, null });

            migrationBuilder.UpdateData(
                table: "detalles_pedido",
                keyColumn: "id",
                keyValue: 7L,
                columns: new[] { "cantidad_entregada", "cantidad_pedida", "cantidad_preparada", "cantidad_rechazada", "motivo_rechazo" },
                values: new object[] { null, 6, 6, null, null });

            migrationBuilder.UpdateData(
                table: "detalles_pedido",
                keyColumn: "id",
                keyValue: 8L,
                columns: new[] { "cantidad_entregada", "cantidad_pedida", "cantidad_preparada", "cantidad_rechazada", "motivo_rechazo" },
                values: new object[] { null, 12, 12, null, null });

            migrationBuilder.UpdateData(
                table: "detalles_pedido",
                keyColumn: "id",
                keyValue: 9L,
                columns: new[] { "cantidad_entregada", "cantidad_pedida", "cantidad_preparada", "cantidad_rechazada", "motivo_rechazo" },
                values: new object[] { null, 4, 4, null, null });

            migrationBuilder.UpdateData(
                table: "detalles_pedido",
                keyColumn: "id",
                keyValue: 10L,
                columns: new[] { "cantidad_entregada", "cantidad_pedida", "cantidad_preparada", "cantidad_rechazada", "motivo_rechazo" },
                values: new object[] { null, 8, 8, null, null });

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "id",
                keyValue: 1L,
                columns: new[] { "auditado_en", "auditado_por_id", "cancelacion_motivo", "cancelacion_solicitada_por_id", "chofer_id", "comprobante_url", "confirmado_en", "confirmado_por_id", "entrega_firma_url", "entrega_foto_url", "entregado_en", "picking_en", "picking_resultado", "validacion_detalle" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "id",
                keyValue: 2L,
                columns: new[] { "auditado_en", "auditado_por_id", "cancelacion_motivo", "cancelacion_solicitada_por_id", "chofer_id", "comprobante_url", "confirmado_en", "confirmado_por_id", "entrega_firma_url", "entrega_foto_url", "entregado_en", "picking_en", "picking_resultado", "validacion_detalle" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "id",
                keyValue: 3L,
                columns: new[] { "auditado_en", "auditado_por_id", "cancelacion_motivo", "cancelacion_solicitada_por_id", "chofer_id", "comprobante_url", "confirmado_en", "confirmado_por_id", "entrega_firma_url", "entrega_foto_url", "entregado_en", "picking_en", "picking_resultado", "validacion_detalle" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "id",
                keyValue: 4L,
                columns: new[] { "auditado_en", "auditado_por_id", "cancelacion_motivo", "cancelacion_solicitada_por_id", "chofer_id", "comprobante_url", "confirmado_en", "confirmado_por_id", "entrega_firma_url", "entrega_foto_url", "entregado_en", "picking_en", "picking_resultado", "validacion_detalle" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "id",
                keyValue: 5L,
                columns: new[] { "auditado_en", "auditado_por_id", "cancelacion_motivo", "cancelacion_solicitada_por_id", "chofer_id", "comprobante_url", "confirmado_en", "confirmado_por_id", "entrega_firma_url", "entrega_foto_url", "entregado_en", "picking_en", "picking_resultado", "validacion_detalle" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_auditado_por_id",
                table: "pedidos",
                column: "auditado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_cancelacion_solicitada_por_id",
                table: "pedidos",
                column: "cancelacion_solicitada_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_chofer_id",
                table: "pedidos",
                column: "chofer_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_confirmado_por_id",
                table: "pedidos",
                column: "confirmado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_cobros_pedido_pedido_id",
                table: "cobros_pedido",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ix_cobros_pedido_registrado_por_id",
                table: "cobros_pedido",
                column: "registrado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_intentos_entrega_chofer_id",
                table: "intentos_entrega",
                column: "chofer_id");

            migrationBuilder.CreateIndex(
                name: "ix_intentos_entrega_pedido_id",
                table: "intentos_entrega",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ix_operaciones_procesadas_id_tipo",
                table: "operaciones_procesadas",
                columns: new[] { "id", "tipo" });

            migrationBuilder.CreateIndex(
                name: "ix_operaciones_procesadas_usuario_id",
                table: "operaciones_procesadas",
                column: "usuario_id");

            migrationBuilder.AddForeignKey(
                name: "fk_pedidos_usuarios_auditado_por_id",
                table: "pedidos",
                column: "auditado_por_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_pedidos_usuarios_cancelacion_solicitada_por_id",
                table: "pedidos",
                column: "cancelacion_solicitada_por_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_pedidos_usuarios_chofer_id",
                table: "pedidos",
                column: "chofer_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_pedidos_usuarios_confirmado_por_id",
                table: "pedidos",
                column: "confirmado_por_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // ── Script de migración de datos estructurados e históricos (Fase 1 / Sección 4) ──
            // 1. Sincronizar cantidades de detalles preexistentes
            migrationBuilder.Sql("UPDATE detalles_pedido SET cantidad_pedida = cantidad, cantidad_preparada = cantidad WHERE cantidad_pedida = 0;");

            // 2. Migrar comprobantes pendientes
            migrationBuilder.Sql(@"
                UPDATE pedidos 
                SET comprobante_estado = 'Pendiente',
                    comprobante_url = SUBSTRING_INDEX(SUBSTRING_INDEX(observaciones, '/uploads/comprobantes/', -1), ']', 1)
                WHERE observaciones LIKE '%[Comprobante de pago: PENDIENTE%' OR observaciones LIKE '%[Comprobante de pago: /uploads/%';
            ");
            migrationBuilder.Sql("UPDATE pedidos SET comprobante_url = CONCAT('/uploads/comprobantes/', comprobante_url) WHERE comprobante_url IS NOT NULL AND NOT comprobante_url LIKE '/uploads/%';");

            // 3. Migrar comprobantes aprobados
            migrationBuilder.Sql(@"
                UPDATE pedidos 
                SET comprobante_estado = 'Aprobado'
                WHERE observaciones LIKE '%[Comprobante de pago: APROBADO%';
            ");

            // 4. Migrar comprobantes rechazados
            migrationBuilder.Sql(@"
                UPDATE pedidos 
                SET comprobante_estado = 'Rechazado'
                WHERE observaciones LIKE '%[Comprobante de pago: RECHAZADO%';
            ");

            // 5. Migrar resultado de picking
            migrationBuilder.Sql("UPDATE pedidos SET picking_resultado = 'Completo' WHERE observaciones LIKE '%[Picking: COMPLETO%';");
            migrationBuilder.Sql("UPDATE pedidos SET picking_resultado = 'Incompleto' WHERE observaciones LIKE '%[Picking: INCOMPLETO%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pedidos_usuarios_auditado_por_id",
                table: "pedidos");

            migrationBuilder.DropForeignKey(
                name: "fk_pedidos_usuarios_cancelacion_solicitada_por_id",
                table: "pedidos");

            migrationBuilder.DropForeignKey(
                name: "fk_pedidos_usuarios_chofer_id",
                table: "pedidos");

            migrationBuilder.DropForeignKey(
                name: "fk_pedidos_usuarios_confirmado_por_id",
                table: "pedidos");

            migrationBuilder.DropTable(
                name: "cobros_pedido");

            migrationBuilder.DropTable(
                name: "intentos_entrega");

            migrationBuilder.DropTable(
                name: "operaciones_procesadas");

            migrationBuilder.DropIndex(
                name: "ix_pedidos_auditado_por_id",
                table: "pedidos");

            migrationBuilder.DropIndex(
                name: "ix_pedidos_cancelacion_solicitada_por_id",
                table: "pedidos");

            migrationBuilder.DropIndex(
                name: "ix_pedidos_chofer_id",
                table: "pedidos");

            migrationBuilder.DropIndex(
                name: "ix_pedidos_confirmado_por_id",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "auditado_en",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "auditado_por_id",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "cancelacion_estado",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "cancelacion_motivo",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "cancelacion_solicitada_por_id",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "chofer_id",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "comprobante_estado",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "comprobante_url",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "confirmado_en",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "confirmado_por_id",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entrega_firma_url",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entrega_foto_url",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entregado_en",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "picking_en",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "picking_resultado",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "validacion_detalle",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "validacion_fallida",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "cantidad_entregada",
                table: "detalles_pedido");

            migrationBuilder.DropColumn(
                name: "cantidad_pedida",
                table: "detalles_pedido");

            migrationBuilder.DropColumn(
                name: "cantidad_preparada",
                table: "detalles_pedido");

            migrationBuilder.DropColumn(
                name: "cantidad_rechazada",
                table: "detalles_pedido");

            migrationBuilder.DropColumn(
                name: "motivo_rechazo",
                table: "detalles_pedido");
        }
    }
}
