using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfisServisSistemi.Migrations
{
    /// <inheritdoc />
    public partial class AidatYetkileriniTemizle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[AidatSorumlusuYetkileri]', N'U') IS NOT NULL
                BEGIN
                    UPDATE [AidatSorumlusuYetkileri]
                    SET [SilindiMi] = 1
                    WHERE [SilindiMi] = 0;
                END

                IF COL_LENGTH(N'Kullanicilar', N'AidatYoneticisiMi') IS NOT NULL
                BEGIN
                    UPDATE [Kullanicilar]
                    SET [AidatYoneticisiMi] = 0
                    WHERE [AidatYoneticisiMi] = 1;
                END

                UPDATE [Kullanicilar]
                SET [Rol] = CASE
                    WHEN [Rol] LIKE N'%KatGorevlisi%' THEN N'KatGorevlisi'
                    ELSE N'Oda'
                END
                WHERE [Rol] LIKE N'%AidatSorumlusu%';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
