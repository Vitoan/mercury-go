using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercuryGo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOfertaYEnPuerta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                                   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'productos' AND COLUMN_NAME = 'en_oferta');
                SET @sql = IF(@col_exists = 0, 'ALTER TABLE `productos` ADD `en_oferta` tinyint(1) NOT NULL DEFAULT FALSE;', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 1L,
                column: "en_oferta",
                value: true);

            migrationBuilder.UpdateData(
                table: "productos",
                keyColumn: "id",
                keyValue: 6L,
                column: "en_oferta",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "en_oferta",
                table: "productos");
        }
    }
}
