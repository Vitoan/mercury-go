using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercuryGo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarNotificacionesYGeolocalizacionEntrega : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_en_puerta",
                table: "pedidos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitud_entrega",
                table: "pedidos",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitud_entrega",
                table: "pedidos",
                type: "double",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notificaciones",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    titulo = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mensaje = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tipo = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "INFO")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    leida = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    referencia_id = table.Column<long>(type: "bigint", nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificaciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_notificaciones_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "id",
                keyValue: 1L,
                columns: new[] { "fecha_en_puerta", "latitud_entrega", "longitud_entrega" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "id",
                keyValue: 2L,
                columns: new[] { "fecha_en_puerta", "latitud_entrega", "longitud_entrega" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "id",
                keyValue: 3L,
                columns: new[] { "fecha_en_puerta", "latitud_entrega", "longitud_entrega" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "id",
                keyValue: 4L,
                columns: new[] { "fecha_en_puerta", "latitud_entrega", "longitud_entrega" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "id",
                keyValue: 5L,
                columns: new[] { "fecha_en_puerta", "latitud_entrega", "longitud_entrega" },
                values: new object[] { null, null, null });

            migrationBuilder.CreateIndex(
                name: "ix_notificaciones_usuario_id",
                table: "notificaciones",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificaciones");

            migrationBuilder.DropColumn(
                name: "fecha_en_puerta",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "latitud_entrega",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "longitud_entrega",
                table: "pedidos");
        }
    }
}
