using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MercuryGo.Api.Migrations
{
    /// <inheritdoc />
    public partial class IdentidadRolesYUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    nombre = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    codigo = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    descripcion = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    activo = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    rol_id = table.Column<long>(type: "bigint", nullable: true),
                    nombre = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    password_hash = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    telefono = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    activo = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuarios_roles_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expira_en = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    revocado_en = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "activo", "actualizado_en", "codigo", "creado_en", "descripcion", "nombre" },
                values: new object[,]
                {
                    { 1L, true, null, "ADMIN", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Control total y auditoría del sistema", "Administrador" },
                    { 2L, true, null, "OPERARIO", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Picking, preparación de bultos y estiba LIFO", "Operario de Depósito" },
                    { 3L, true, null, "CHOFER", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Hoja de ruta, entrega en destino y cobranza", "Chofer Repartidor" },
                    { 4L, true, null, "CLIENTE", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Autogestión de compras y seguimiento de remitos", "Comercio / Cliente B2B" }
                });

            migrationBuilder.InsertData(
                table: "usuarios",
                columns: new[] { "id", "activo", "actualizado_en", "creado_en", "email", "nombre", "password_hash", "rol_id", "telefono" },
                values: new object[,]
                {
                    { 5L, true, null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "nuevo@mercurygo.local", "Usuario Nuevo Pendiente", "AQAAAAIAAYagAAAAEKiKxpDQBOt5YojXQ0cuDHJ/9Zt+6ShbGrXgpAh7vLC+ueHYS+BZCJS9OY/RPkQmIQ==", null, "266-4009900" },
                    { 1L, true, null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@mercurygo.local", "Administrador Central", "AQAAAAIAAYagAAAAEKiKxpDQBOt5YojXQ0cuDHJ/9Zt+6ShbGrXgpAh7vLC+ueHYS+BZCJS9OY/RPkQmIQ==", 1L, "266-4001122" },
                    { 2L, true, null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "operario@mercurygo.local", "Operario de Picking", "AQAAAAIAAYagAAAAEKiKxpDQBOt5YojXQ0cuDHJ/9Zt+6ShbGrXgpAh7vLC+ueHYS+BZCJS9OY/RPkQmIQ==", 2L, "266-4003344" },
                    { 3L, true, null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "chofer@mercurygo.local", "Chofer Reparto 01", "AQAAAAIAAYagAAAAEKiKxpDQBOt5YojXQ0cuDHJ/9Zt+6ShbGrXgpAh7vLC+ueHYS+BZCJS9OY/RPkQmIQ==", 3L, "266-4005566" },
                    { 4L, true, null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "cliente@mercurygo.local", "Cliente Autoservicio San Luis", "AQAAAAIAAYagAAAAEKiKxpDQBOt5YojXQ0cuDHJ/9Zt+6ShbGrXgpAh7vLC+ueHYS+BZCJS9OY/RPkQmIQ==", 4L, "266-4007788" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_usuario_id_expira_en",
                table: "refresh_tokens",
                columns: new[] { "usuario_id", "expira_en" });

            migrationBuilder.CreateIndex(
                name: "ix_roles_codigo",
                table: "roles",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_email",
                table: "usuarios",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_rol_id",
                table: "usuarios",
                column: "rol_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "roles");
        }
    }
}
