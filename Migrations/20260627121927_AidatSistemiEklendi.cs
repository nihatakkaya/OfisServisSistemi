using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfisServisSistemi.Migrations
{
    /// <inheritdoc />
    public partial class AidatSistemiEklendi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kantinler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SilindiMi = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kantinler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Aidatlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KantinId = table.Column<int>(type: "int", nullable: false),
                    KullaniciId = table.Column<int>(type: "int", nullable: false),
                    Miktar = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OdemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AyYil = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SilindiMi = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aidatlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aidatlar_Kantinler_KantinId",
                        column: x => x.KantinId,
                        principalTable: "Kantinler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Aidatlar_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KantinKullanicilari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KantinId = table.Column<int>(type: "int", nullable: false),
                    KullaniciId = table.Column<int>(type: "int", nullable: false),
                    SilindiMi = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KantinKullanicilari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KantinKullanicilari_Kantinler_KantinId",
                        column: x => x.KantinId,
                        principalTable: "Kantinler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KantinKullanicilari_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aidatlar_KantinId",
                table: "Aidatlar",
                column: "KantinId");

            migrationBuilder.CreateIndex(
                name: "IX_Aidatlar_KullaniciId",
                table: "Aidatlar",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_KantinKullanicilari_KantinId",
                table: "KantinKullanicilari",
                column: "KantinId");

            migrationBuilder.CreateIndex(
                name: "IX_KantinKullanicilari_KullaniciId",
                table: "KantinKullanicilari",
                column: "KullaniciId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aidatlar");

            migrationBuilder.DropTable(
                name: "KantinKullanicilari");

            migrationBuilder.DropTable(
                name: "Kantinler");
        }
    }
}
