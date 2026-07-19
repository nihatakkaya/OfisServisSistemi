using Microsoft.EntityFrameworkCore;
using OfisServisSistemi.Models;

namespace OfisServisSistemi.Data
{
    public static class DatabaseInitializer
    {
        private const int MaxRetryCount = 30;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

        public static async Task MigrateAndSeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("DatabaseInitializer");

            for (var attempt = 1; attempt <= MaxRetryCount; attempt++)
            {
                try
                {
                    await db.Database.MigrateAsync();
                    await SeedAsync(db);

                    logger.LogInformation("Database migration and seed completed.");
                    return;
                }
                catch (Exception ex) when (attempt < MaxRetryCount)
                {
                    logger.LogWarning(
                        ex,
                        "Database is not ready or initialization failed. Retrying {Attempt}/{MaxRetryCount}.",
                        attempt,
                        MaxRetryCount);

                    await Task.Delay(RetryDelay);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Database migration and seed failed.");
                    throw;
                }
            }
        }

        private static async Task SeedAsync(ApplicationDbContext db)
        {
            var strategy = db.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync();

                await EnsureUserAsync(db, "superadmin", "123456", "SuperAdmin", null);

                var bina = await EnsureBinaAsync(db, "T1 Binası");
                var kat3 = await EnsureKatAsync(db, "3. Kat", bina.Id);
                var kat2 = await EnsureKatAsync(db, "2. Kat", bina.Id);

                var cayci3 = await EnsureUserAsync(db, "cayci3", "1234", "KatGorevlisi", kat3.Id);
                await EnsureKullaniciOdaAsync(db, cayci3.Id, kat3.Id, null);

                for (var oda = 301; oda <= 305; oda++)
                {
                    var odaNo = oda.ToString();
                    var odaKullanici = await EnsureUserAsync(db, odaNo, odaNo, "Oda", kat3.Id);
                    await EnsureKullaniciOdaAsync(db, odaKullanici.Id, kat3.Id, odaNo);
                }

                var cayci2 = await EnsureUserAsync(db, "cayci2", "1234", "KatGorevlisi", kat2.Id);
                await EnsureKullaniciOdaAsync(db, cayci2.Id, kat2.Id, null);

                for (var oda = 201; oda <= 203; oda++)
                {
                    var odaNo = oda.ToString();
                    var odaKullanici = await EnsureUserAsync(db, odaNo, odaNo, "Oda", kat2.Id);
                    await EnsureKullaniciOdaAsync(db, odaKullanici.Id, kat2.Id, odaNo);
                }

                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            });
        }

        private static async Task<Bina> EnsureBinaAsync(ApplicationDbContext db, string ad)
        {
            var bina = await db.Binalar.FirstOrDefaultAsync(x => x.Ad == ad);

            if (bina is not null)
            {
                return bina;
            }

            bina = new Bina { Ad = ad };
            db.Binalar.Add(bina);
            await db.SaveChangesAsync();

            return bina;
        }

        private static async Task<Kat> EnsureKatAsync(ApplicationDbContext db, string ad, int binaId)
        {
            var kat = await db.Katlar.FirstOrDefaultAsync(x => x.Ad == ad && x.BinaId == binaId);

            if (kat is not null)
            {
                return kat;
            }

            kat = new Kat { Ad = ad, BinaId = binaId };
            db.Katlar.Add(kat);
            await db.SaveChangesAsync();

            return kat;
        }

        private static async Task<Kullanici> EnsureUserAsync(
            ApplicationDbContext db,
            string kullaniciAdi,
            string sifre,
            string rol,
            int? katId)
        {
            var kullanici = await db.Kullanicilar
                .FirstOrDefaultAsync(x => x.KullaniciAdi == kullaniciAdi);

            if (kullanici is not null)
            {
                return kullanici;
            }

            kullanici = new Kullanici
            {
                KullaniciAdi = kullaniciAdi,
                Sifre = sifre,
                Rol = rol,
                KatId = katId
            };

            db.Kullanicilar.Add(kullanici);
            await db.SaveChangesAsync();

            return kullanici;
        }

        private static async Task EnsureKullaniciOdaAsync(
            ApplicationDbContext db,
            int kullaniciId,
            int katId,
            string? odaNumarasi)
        {
            var exists = await db.KullaniciOdalari.AnyAsync(x =>
                x.KullaniciId == kullaniciId &&
                x.KatId == katId &&
                x.OdaNumarasi == odaNumarasi);

            if (exists)
            {
                return;
            }

            db.KullaniciOdalari.Add(new KullaniciOda
            {
                KullaniciId = kullaniciId,
                KatId = katId,
                OdaNumarasi = odaNumarasi
            });
        }
    }
}
