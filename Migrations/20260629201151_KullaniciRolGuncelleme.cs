using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfisServisSistemi.Migrations
{
    /// <inheritdoc />
    public partial class KullaniciRolGuncelleme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AidatYoneticisiMi",
                table: "Kullanicilar",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AidatYoneticisiMi",
                table: "Kullanicilar");
        }
    }
}
