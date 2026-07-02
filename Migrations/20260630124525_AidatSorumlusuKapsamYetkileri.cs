using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfisServisSistemi.Migrations
{
    /// <inheritdoc />
    public partial class AidatSorumlusuKapsamYetkileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AidatSorumlusuYetkileri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KantinId = table.Column<int>(type: "int", nullable: false),
                    KullaniciId = table.Column<int>(type: "int", nullable: false),
                    BinaId = table.Column<int>(type: "int", nullable: true),
                    KatId = table.Column<int>(type: "int", nullable: true),
                    SilindiMi = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidatSorumlusuYetkileri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AidatSorumlusuYetkileri_Binalar_BinaId",
                        column: x => x.BinaId,
                        principalTable: "Binalar",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AidatSorumlusuYetkileri_Kantinler_KantinId",
                        column: x => x.KantinId,
                        principalTable: "Kantinler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AidatSorumlusuYetkileri_Katlar_KatId",
                        column: x => x.KatId,
                        principalTable: "Katlar",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AidatSorumlusuYetkileri_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AidatSorumlusuYetkileri_BinaId",
                table: "AidatSorumlusuYetkileri",
                column: "BinaId");

            migrationBuilder.CreateIndex(
                name: "IX_AidatSorumlusuYetkileri_KantinId",
                table: "AidatSorumlusuYetkileri",
                column: "KantinId");

            migrationBuilder.CreateIndex(
                name: "IX_AidatSorumlusuYetkileri_KatId",
                table: "AidatSorumlusuYetkileri",
                column: "KatId");

            migrationBuilder.CreateIndex(
                name: "IX_AidatSorumlusuYetkileri_KullaniciId",
                table: "AidatSorumlusuYetkileri",
                column: "KullaniciId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AidatSorumlusuYetkileri");
        }
    }
}
