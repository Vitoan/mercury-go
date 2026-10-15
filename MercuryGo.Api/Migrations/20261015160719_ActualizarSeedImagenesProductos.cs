using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercuryGo.Api.Migrations
{
    /// <inheritdoc />
    public partial class ActualizarSeedImagenesProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 1L,
                column: "imagen_url",
                value: "/seed/productos/aceite.jpg");

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 2L,
                column: "imagen_url",
                value: "/seed/productos/harina.jpg");

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 3L,
                column: "imagen_url",
                value: "/seed/productos/arroz.jpg");

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 4L,
                column: "imagen_url",
                value: "/seed/productos/fideos.jpg");

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 5L,
                column: "imagen_url",
                value: "/seed/productos/azucar.jpg");

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 6L,
                column: "imagen_url",
                value: "/seed/productos/yerba.jpg");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 1L,
                column: "imagen_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 2L,
                column: "imagen_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 3L,
                column: "imagen_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 4L,
                column: "imagen_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 5L,
                column: "imagen_url",
                value: null);

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 6L,
                column: "imagen_url",
                value: null);
        }
    }
}
