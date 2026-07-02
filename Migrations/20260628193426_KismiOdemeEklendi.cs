using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfisServisSistemi.Migrations
{
    /// <inheritdoc />
    public partial class KismiOdemeEklendi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AylikSabitTutar",
                table: "Kantinler",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AylikSabitTutar",
                table: "Kantinler");
        }
    }
}
