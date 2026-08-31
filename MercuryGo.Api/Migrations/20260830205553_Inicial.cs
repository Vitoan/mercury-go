using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MercuryGo.Api.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "categorias",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    descripcion = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    activo = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categorias", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "productos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    categoria_id = table.Column<long>(type: "bigint", nullable: false),
                    nombre = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    descripcion = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    precio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    imagen_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    disponible = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    activo = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_productos", x => x.id);
                    table.ForeignKey(
                        name: "fk_productos_categorias_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "categorias",
                columns: new[] { "id", "activo", "actualizado_en", "creado_en", "descripcion", "nombre" },
                values: new object[,]
                {
                    { 1L, true, null, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Aceites, azúcares y productos secos", "Almacén y Comestibles" },
                    { 2L, true, null, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Harinas, fideos y sémolas", "Harinas y Pastas" },
                    { 3L, true, null, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Yerba mate, té y café", "Infusiones" }
                });

            migrationBuilder.InsertData(
                table: "productos",
                columns: new[] { "id", "activo", "actualizado_en", "categoria_id", "creado_en", "descripcion", "disponible", "imagen_url", "nombre", "precio" },
                values: new object[,]
                {
                    { 1L, true, null, 1L, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Caja x 12 unidades", true, null, "Aceite de Girasol 1.5L", 2400.00m },
                    { 2L, true, null, 2L, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Pack x 10 unidades", true, null, "Harina 000 1kg", 950.00m }
                });

            migrationBuilder.InsertData(
                table: "productos",
                columns: new[] { "id", "activo", "actualizado_en", "categoria_id", "creado_en", "descripcion", "imagen_url", "nombre", "precio" },
                values: new object[] { 3L, true, null, 1L, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Bolsa x 10 unidades", null, "Arroz Largo Fino 1kg", 1800.00m });

            migrationBuilder.InsertData(
                table: "productos",
                columns: new[] { "id", "activo", "actualizado_en", "categoria_id", "creado_en", "descripcion", "disponible", "imagen_url", "nombre", "precio" },
                values: new object[,]
                {
                    { 4L, true, null, 2L, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Caja x 20 paquetes", true, null, "Fideos Guiseros 500g", 1100.00m },
                    { 5L, true, null, 1L, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Fardo x 10 unidades", true, null, "Azúcar Común 1kg", 1250.00m },
                    { 6L, true, null, 3L, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Pack x 6 unidades", true, null, "Yerba Mate 1kg", 3400.00m }
                });

            migrationBuilder.CreateIndex(
                name: "ix_productos_categoria_id",
                table: "productos",
                column: "categoria_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "productos");

            migrationBuilder.DropTable(
                name: "categorias");
        }
    }
}
