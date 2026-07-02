using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfisServisSistemi.Data;
using OfisServisSistemi.Hubs;
using OfisServisSistemi.Models;
using OfisServisSistemi.ViewModels;
using System.Text.Json;

namespace OfisServisSistemi.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<OfisHub> _hubContext;

        public HomeController(ApplicationDbContext context, IHubContext<OfisHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private void LogKaydet(string islemTuru, string detay)
        {
            var aktifKullanici = User.Identity?.Name ?? "Sistem";
            _context.SistemLoglari.Add(new SistemLog
            {
                KullaniciAdi = aktifKullanici,
                IslemTuru = islemTuru,
                Detay = detay,
                Tarih = DateTime.Now
            });
            _context.SaveChanges();
        }

        private async Task OtomatikOnaylariKontrolEt()
        {
            var zamanSiniri = DateTime.Now.AddMinutes(-120);

            var unutulanTalepler = await _context.Talepler
                .Where(t => t.Durum == "TeslimEdildi" && t.OlusturulmaTarihi <= zamanSiniri)
                .ToListAsync();

            if (unutulanTalepler.Any())
            {
                foreach (var talep in unutulanTalepler)
                {
                    talep.Durum = "Tamamlandi";
                }
                await _context.SaveChangesAsync();
            }
        }

        private async Task<List<int>> YetkiliKantinIdleri(Kullanici user)
        {
            if (User.IsInRole("SuperAdmin"))
            {
                return await _context.Kantinler
                    .Where(k => !k.SilindiMi)
                    .Select(k => k.Id)
                    .ToListAsync();
            }

            if (!User.IsInRole("AidatSorumlusu")) return new List<int>();

            return await _context.AidatSorumlusuYetkileri
                .Where(y => y.KullaniciId == user.Id && !y.SilindiMi && !y.Kantin.SilindiMi)
                .Select(y => y.KantinId)
                .Distinct()
                .ToListAsync();
        }

        private async Task<bool> KantinAidatYetkisiVar(Kullanici user, int kantinId)
        {
            if (User.IsInRole("SuperAdmin")) return true;
            if (!User.IsInRole("AidatSorumlusu")) return false;

            return await _context.AidatSorumlusuYetkileri
                .AnyAsync(y => y.KantinId == kantinId && y.KullaniciId == user.Id && !y.SilindiMi && !y.Kantin.SilindiMi);
        }

        private async Task<bool> KullaniciTahsilYetkisiVar(Kullanici sorumlu, int kantinId, int hedefKullaniciId)
        {
            if (User.IsInRole("SuperAdmin")) return true;
            if (!await KantinAidatYetkisiVar(sorumlu, kantinId)) return false;

            var yetkiler = await _context.AidatSorumlusuYetkileri
                .Where(y => y.KantinId == kantinId && y.KullaniciId == sorumlu.Id && !y.SilindiMi)
                .ToListAsync();

            var katIdleri = yetkiler.Where(y => y.KatId.HasValue).Select(y => y.KatId!.Value).ToList();
            var binaIdleri = yetkiler.Where(y => y.BinaId.HasValue).Select(y => y.BinaId!.Value).ToList();

            return await _context.KullaniciOdalari
                .AnyAsync(ko => ko.KullaniciId == hedefKullaniciId
                             && !ko.SilindiMi
                             && !ko.Kat.SilindiMi
                             && (katIdleri.Contains(ko.KatId) || binaIdleri.Contains(ko.Kat.BinaId)));
        }

        private async Task<List<int>> AidatKapsamindakiKullaniciIdleri(Kullanici sorumlu, int kantinId)
        {
            var uyelerQuery = _context.KantinKullanicilari
                .Include(kk => kk.Kullanici)
                .Where(kk => kk.KantinId == kantinId && !kk.SilindiMi && kk.Kullanici.Rol != "AidatSorumlusu");

            if (!User.IsInRole("SuperAdmin"))
            {
                var yetkiler = await _context.AidatSorumlusuYetkileri
                    .Where(y => y.KantinId == kantinId && y.KullaniciId == sorumlu.Id && !y.SilindiMi)
                    .ToListAsync();

                var katIdleri = yetkiler.Where(y => y.KatId.HasValue).Select(y => y.KatId!.Value).ToList();
                var binaIdleri = yetkiler.Where(y => y.BinaId.HasValue).Select(y => y.BinaId!.Value).ToList();

                uyelerQuery = uyelerQuery.Where(kk => _context.KullaniciOdalari
                    .Any(ko => ko.KullaniciId == kk.KullaniciId
                            && !ko.SilindiMi
                            && !ko.Kat.SilindiMi
                            && (katIdleri.Contains(ko.KatId) || binaIdleri.Contains(ko.Kat.BinaId))));
            }

            return await uyelerQuery
                .Select(kk => kk.KullaniciId)
                .Distinct()
                .ToListAsync();
        }

        private static string CsvDeger(string? deger)
        {
            return string.IsNullOrWhiteSpace(deger)
                ? ""
                : deger.Replace(";", ",").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private IActionResult VarsayilanEkranaDon()
        {
            if (User.IsInRole("KatGorevlisi")) return RedirectToAction("Index");
            return RedirectToAction("Oda");
        }

        public async Task<IActionResult> Index()
        {
            await OtomatikOnaylariKontrolEt();

            if (!User.IsInRole("KatGorevlisi")) return RedirectToAction("Oda");
            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            if (user == null) return RedirectToAction("Login", "Account");

            var gorevliBaglantilari = await _context.KullaniciOdalari
                .Include(ko => ko.Kat)
                .Where(ko => ko.KullaniciId == user.Id && !ko.SilindiMi && !ko.Kat.SilindiMi)
                .ToListAsync();

            if (!gorevliBaglantilari.Any()) return RedirectToAction("Login", "Account");

            var gorevliKatIdleri = gorevliBaglantilari.Select(ko => ko.KatId).ToList();

            var aktifOdalar = await _context.KullaniciOdalari
                .Where(ko => gorevliKatIdleri.Contains(ko.KatId) && ko.OdaNumarasi != null && !ko.SilindiMi && !ko.Kat.SilindiMi)
                .Select(ko => ko.OdaNumarasi)
                .Distinct()
                .Select(odaNo => new Kullanici { KullaniciAdi = odaNo, OdaNumarasi = odaNo })
                .ToListAsync();

            var gecerliOdaNumaralari = aktifOdalar.Select(o => o.OdaNumarasi).ToList();

            var aktifTalepler = await _context.Talepler
                .Where(t => gorevliKatIdleri.Contains(t.KatId)
                         && (t.Durum == "Bekliyor" || t.Durum == "TeslimEdildi")
                         && gecerliOdaNumaralari.Contains(t.OdaAdi))
                .ToListAsync();

            var katUrunleri = await _context.Urunler
                .Where(u => gorevliKatIdleri.Contains(u.KatId) && !u.SilindiMi)
                .ToListAsync();

            var model = new KatDurumModel { Odalar = aktifOdalar, AktifTalepler = aktifTalepler, Urunler = katUrunleri };

            ViewBag.KatAdi = string.Join(", ", gorevliBaglantilari.Select(gb => gb.Kat.Ad).Distinct());
            ViewBag.AidatYetkiliMi = (await YetkiliKantinIdleri(user)).Any();
            return View(model);
        }

        public async Task<IActionResult> AidatTakip(string ayYil, int? kantinId)
        {
            if (!User.IsInRole("AidatSorumlusu") && !User.IsInRole("SuperAdmin")) return Unauthorized();

            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            if (user == null) return RedirectToAction("Login", "Account");

            var yetkiliKantinler = await YetkiliKantinIdleri(user);

            if (!yetkiliKantinler.Any())
            {
                TempData["Hata"] = "Herhangi bir kantine atanmamışsınız. Lütfen sistem yöneticisiyle görüşün.";
                return VarsayilanEkranaDon();
            }

            int aktifKantinId = kantinId.HasValue && yetkiliKantinler.Contains(kantinId.Value)
                ? kantinId.Value
                : yetkiliKantinler.First();

            var kantin = await _context.Kantinler.FirstOrDefaultAsync(k => k.Id == aktifKantinId);
            var kantinSecenekleri = await _context.Kantinler
                .Where(k => yetkiliKantinler.Contains(k.Id) && !k.SilindiMi)
                .OrderBy(k => k.Ad)
                .ToListAsync();

            string gecerliAyYil = string.IsNullOrEmpty(ayYil) ? DateTime.Now.ToString("yyyy-MM") : ayYil;

            var uyelerQuery = _context.KantinKullanicilari
                .Include(kk => kk.Kullanici)
                .Where(kk => kk.KantinId == aktifKantinId && !kk.SilindiMi && kk.Kullanici.Rol != "AidatSorumlusu");

            if (!User.IsInRole("SuperAdmin"))
            {
                var yetkiler = await _context.AidatSorumlusuYetkileri
                    .Where(y => y.KantinId == aktifKantinId && y.KullaniciId == user.Id && !y.SilindiMi)
                    .ToListAsync();

                var katIdleri = yetkiler.Where(y => y.KatId.HasValue).Select(y => y.KatId!.Value).ToList();
                var binaIdleri = yetkiler.Where(y => y.BinaId.HasValue).Select(y => y.BinaId!.Value).ToList();

                uyelerQuery = uyelerQuery.Where(kk => _context.KullaniciOdalari
                    .Any(ko => ko.KullaniciId == kk.KullaniciId
                            && !ko.SilindiMi
                            && !ko.Kat.SilindiMi
                            && (katIdleri.Contains(ko.KatId) || binaIdleri.Contains(ko.Kat.BinaId))));
            }

            var uyeler = await uyelerQuery
                .OrderBy(kk => kk.Kullanici.KullaniciAdi)
                .ToListAsync();

            var gorunenKullaniciIdleri = uyeler.Select(u => u.KullaniciId).Distinct().ToList();

            var odenenAidatlar = await _context.Aidatlar
                .Where(a => a.KantinId == aktifKantinId
                         && a.AyYil == gecerliAyYil
                         && !a.SilindiMi
                         && gorunenKullaniciIdleri.Contains(a.KullaniciId))
                .ToListAsync();

            var giderler = await _context.AidatGiderleri
                .Where(g => g.KantinId == aktifKantinId && g.AyYil == gecerliAyYil && !g.SilindiMi)
                .OrderByDescending(g => g.Tarih)
                .ToListAsync();

            decimal toplamToplananAidat = odenenAidatlar.Sum(a => a.Miktar);
            decimal toplamGider = giderler.Sum(g => g.Miktar);

            ViewBag.KantinAdi = kantin?.Ad;
            ViewBag.KantinAylikTutar = kantin?.AylikSabitTutar ?? 0;
            ViewBag.SecilenAyYil = gecerliAyYil;
            ViewBag.KantinId = aktifKantinId;
            ViewBag.KantinSecenekleri = kantinSecenekleri;
            ViewBag.OdenenAidatlar = odenenAidatlar;
            ViewBag.AidatGiderleri = giderler;
            ViewBag.ToplananAidat = toplamToplananAidat;
            ViewBag.ToplamGider = toplamGider;
            ViewBag.KasaBakiyesi = toplamToplananAidat - toplamGider;
            ViewBag.GeriDonusUrl = User.IsInRole("KatGorevlisi") ? "/Home/Index" : "/Home/Oda";

            return View(uyeler);
        }

        public async Task<IActionResult> AidatHarcamaRapor(string ayYil, int? kantinId)
        {
            if (!User.IsInRole("AidatSorumlusu") && !User.IsInRole("SuperAdmin")) return Unauthorized();

            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            if (user == null) return RedirectToAction("Login", "Account");

            var yetkiliKantinler = await YetkiliKantinIdleri(user);

            if (!yetkiliKantinler.Any())
            {
                TempData["Hata"] = "Herhangi bir kantine atanmamışsınız. Lütfen sistem yöneticisiyle görüşün.";
                return VarsayilanEkranaDon();
            }

            int aktifKantinId = kantinId.HasValue && yetkiliKantinler.Contains(kantinId.Value)
                ? kantinId.Value
                : yetkiliKantinler.First();

            var kantin = await _context.Kantinler.FirstOrDefaultAsync(k => k.Id == aktifKantinId && !k.SilindiMi);
            if (kantin == null) return VarsayilanEkranaDon();

            var kantinSecenekleri = await _context.Kantinler
                .Where(k => yetkiliKantinler.Contains(k.Id) && !k.SilindiMi)
                .OrderBy(k => k.Ad)
                .ToListAsync();

            string raporDonemi = string.IsNullOrEmpty(ayYil) ? DateTime.Now.ToString("yyyy-MM") : ayYil;

            var uyelerQuery = _context.KantinKullanicilari
                .Include(kk => kk.Kullanici)
                .Where(kk => kk.KantinId == aktifKantinId && !kk.SilindiMi && kk.Kullanici.Rol != "AidatSorumlusu");

            if (!User.IsInRole("SuperAdmin"))
            {
                var yetkiler = await _context.AidatSorumlusuYetkileri
                    .Where(y => y.KantinId == aktifKantinId && y.KullaniciId == user.Id && !y.SilindiMi)
                    .ToListAsync();

                var katIdleri = yetkiler.Where(y => y.KatId.HasValue).Select(y => y.KatId!.Value).ToList();
                var binaIdleri = yetkiler.Where(y => y.BinaId.HasValue).Select(y => y.BinaId!.Value).ToList();

                uyelerQuery = uyelerQuery.Where(kk => _context.KullaniciOdalari
                    .Any(ko => ko.KullaniciId == kk.KullaniciId
                            && !ko.SilindiMi
                            && !ko.Kat.SilindiMi
                            && (katIdleri.Contains(ko.KatId) || binaIdleri.Contains(ko.Kat.BinaId))));
            }

            var gorunenKullaniciIdleri = await uyelerQuery
                .Select(kk => kk.KullaniciId)
                .Distinct()
                .ToListAsync();

            var raporAidatlari = await _context.Aidatlar
                .Include(a => a.Kullanici)
                .Where(a => a.KantinId == aktifKantinId
                         && a.AyYil == raporDonemi
                         && !a.SilindiMi
                         && gorunenKullaniciIdleri.Contains(a.KullaniciId))
                .OrderBy(a => a.Kullanici.KullaniciAdi)
                .ThenBy(a => a.OdemeTarihi)
                .ToListAsync();

            var raporGiderleri = await _context.AidatGiderleri
                .Where(g => g.KantinId == aktifKantinId && g.AyYil == raporDonemi && !g.SilindiMi)
                .OrderBy(g => g.Tarih)
                .ToListAsync();

            decimal raporToplananAidat = raporAidatlari.Sum(a => a.Miktar);
            decimal raporToplamGider = raporGiderleri.Sum(g => g.Miktar);

            ViewBag.KantinAdi = kantin.Ad;
            ViewBag.KantinId = aktifKantinId;
            ViewBag.KantinSecenekleri = kantinSecenekleri;
            ViewBag.RaporAyYil = raporDonemi;
            ViewBag.RaporAidatlari = raporAidatlari;
            ViewBag.RaporGiderleri = raporGiderleri;
            ViewBag.RaporToplananAidat = raporToplananAidat;
            ViewBag.RaporToplamGider = raporToplamGider;
            ViewBag.RaporKasaBakiyesi = raporToplananAidat - raporToplamGider;
            ViewBag.GeriDonusUrl = Url.Action("AidatTakip", "Home", new { ayYil = raporDonemi, kantinId = aktifKantinId }) ?? "/Home/AidatTakip";

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AidatHarcamaExcelAktar(string ayYil, int? kantinId)
        {
            if (!User.IsInRole("AidatSorumlusu") && !User.IsInRole("SuperAdmin")) return Unauthorized();

            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            if (user == null) return RedirectToAction("Login", "Account");

            var yetkiliKantinler = await YetkiliKantinIdleri(user);
            if (!yetkiliKantinler.Any()) return Unauthorized();

            int aktifKantinId = kantinId.HasValue && yetkiliKantinler.Contains(kantinId.Value)
                ? kantinId.Value
                : yetkiliKantinler.First();

            var kantin = await _context.Kantinler.FirstOrDefaultAsync(k => k.Id == aktifKantinId && !k.SilindiMi);
            if (kantin == null) return NotFound();

            string raporDonemi = string.IsNullOrEmpty(ayYil) ? DateTime.Now.ToString("yyyy-MM") : ayYil;
            var gorunenKullaniciIdleri = await AidatKapsamindakiKullaniciIdleri(user, aktifKantinId);

            var raporAidatlari = await _context.Aidatlar
                .Include(a => a.Kullanici)
                .Where(a => a.KantinId == aktifKantinId
                         && a.AyYil == raporDonemi
                         && !a.SilindiMi
                         && gorunenKullaniciIdleri.Contains(a.KullaniciId))
                .OrderBy(a => a.Kullanici.KullaniciAdi)
                .ThenBy(a => a.OdemeTarihi)
                .ToListAsync();

            var raporGiderleri = await _context.AidatGiderleri
                .Where(g => g.KantinId == aktifKantinId && g.AyYil == raporDonemi && !g.SilindiMi)
                .OrderBy(g => g.Tarih)
                .ToListAsync();

            decimal raporToplananAidat = raporAidatlari.Sum(a => a.Miktar);
            decimal raporToplamGider = raporGiderleri.Sum(g => g.Miktar);
            decimal raporKasaBakiyesi = raporToplananAidat - raporToplamGider;

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Rapor Ozeti");
            builder.AppendLine("Kantin;Donem;Toplanan Aidat (TL);Yapilan Harcama (TL);Kalan Para (TL)");
            builder.AppendLine($"{CsvDeger(kantin.Ad)};{raporDonemi};{raporToplananAidat};{raporToplamGider};{raporKasaBakiyesi}");
            builder.AppendLine();
            builder.AppendLine("Toplanan Aidatlar");
            builder.AppendLine("Personel / Oda;Odenen Tutar (TL);Ait Oldugu Ay;Odeme Tarihi;Aciklama");

            foreach (var aidat in raporAidatlari)
            {
                builder.AppendLine($"{CsvDeger(aidat.Kullanici?.KullaniciAdi)};{aidat.Miktar};{aidat.AyYil};{aidat.OdemeTarihi:dd.MM.yyyy HH:mm};{CsvDeger(aidat.Aciklama)}");
            }

            builder.AppendLine();
            builder.AppendLine("Yapilan Harcamalar");
            builder.AppendLine("Harcama Aciklamasi;Kaydeden;Tarih;Tutar (TL)");

            foreach (var gider in raporGiderleri)
            {
                builder.AppendLine($"{CsvDeger(gider.Aciklama)};{CsvDeger(gider.KaydedenKullaniciAdi)};{gider.Tarih:dd.MM.yyyy HH:mm};{gider.Miktar}");
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
            string kantinAdi = CsvDeger(kantin.Ad).Replace(" ", "_");
            string fileName = $"Aidat_Harcama_Raporu_{kantinAdi}_{raporDonemi}.csv";

            return File(bytes, "text/csv", fileName);
        }

        [HttpPost]
        public async Task<IActionResult> AidatOde(int kantinId, int kullaniciId, decimal miktar, string ayYil, string aciklama)
        {
            if (miktar <= 0) return BadRequest("Miktar 0'dan büyük olmalıdır.");

            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            if (user == null || !await KullaniciTahsilYetkisiVar(user, kantinId, kullaniciId)) return Unauthorized();

            string donem = string.IsNullOrWhiteSpace(ayYil) ? DateTime.Now.ToString("yyyy-MM") : ayYil;
            string aciklamaMetni = string.IsNullOrWhiteSpace(aciklama) ? "Elden Nakit Ödendi" : aciklama.Trim();

            var aidatKaydi = await _context.Aidatlar
                .Where(a => a.KantinId == kantinId
                         && a.KullaniciId == kullaniciId
                         && a.AyYil == donem
                         && a.SilindiMi)
                .OrderByDescending(a => a.OdemeTarihi)
                .FirstOrDefaultAsync();

            if (aidatKaydi == null)
            {
                aidatKaydi = new Aidat
                {
                    KantinId = kantinId,
                    KullaniciId = kullaniciId,
                    AyYil = donem
                };

                _context.Aidatlar.Add(aidatKaydi);
            }

            aidatKaydi.Miktar = miktar;
            aidatKaydi.Aciklama = aciklamaMetni;
            aidatKaydi.OdemeTarihi = DateTime.Now;
            aidatKaydi.SilindiMi = false;

            await _context.SaveChangesAsync();

            var kantin = await _context.Kantinler.FindAsync(kantinId);
            var odenenKisi = await _context.Kullanicilar.FindAsync(kullaniciId);
            LogKaydet("💰 Tahsilat Alındı", $"'{kantin?.Ad}' kasasına '{odenenKisi?.KullaniciAdi}' adlı personelden {miktar:N0} TL tahsilat yapıldı. (Dönem: {aidatKaydi.AyYil})");

            TempData["Basari"] = "Aidat başarıyla kaydedildi.";
            return RedirectToAction("AidatTakip", new { ayYil = aidatKaydi.AyYil, kantinId = aidatKaydi.KantinId });
        }

        [HttpPost]
        public async Task<IActionResult> AidatGiderEkle(int kantinId, string ayYil, decimal miktar, string aciklama)
        {
            if (miktar <= 0)
            {
                TempData["Hata"] = "Gider tutarı 0'dan büyük olmalıdır.";
                return RedirectToAction("AidatTakip", new { ayYil, kantinId });
            }

            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            if (user == null || !await KantinAidatYetkisiVar(user, kantinId)) return Unauthorized();

            string donem = string.IsNullOrWhiteSpace(ayYil) ? DateTime.Now.ToString("yyyy-MM") : ayYil;

            var gider = new AidatGider
            {
                KantinId = kantinId,
                AyYil = donem,
                Miktar = miktar,
                Aciklama = string.IsNullOrWhiteSpace(aciklama) ? "Gider" : aciklama.Trim(),
                KaydedenKullaniciAdi = username ?? "Sistem",
                Tarih = DateTime.Now
            };

            _context.AidatGiderleri.Add(gider);
            await _context.SaveChangesAsync();

            var kantin = await _context.Kantinler.FindAsync(kantinId);
            LogKaydet("Aidat Gideri Eklendi", $"'{kantin?.Ad}' için {donem} dönemine {miktar:N0} TL gider eklendi. Açıklama: {gider.Aciklama}");

            TempData["Basari"] = "Gider kaydı eklendi.";
            return RedirectToAction("AidatTakip", new { ayYil = donem, kantinId });
        }

        [HttpPost]
        public async Task<IActionResult> AidatGiderSil(int id)
        {
            var gider = await _context.AidatGiderleri
                .Include(g => g.Kantin)
                .FirstOrDefaultAsync(g => g.Id == id && !g.SilindiMi);

            if (gider == null)
            {
                TempData["Hata"] = "Gider kaydı bulunamadı veya zaten silinmiş.";
                return RedirectToAction("AidatTakip");
            }

            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            if (user == null || !await KantinAidatYetkisiVar(user, gider.KantinId)) return Unauthorized();

            gider.SilindiMi = true;
            await _context.SaveChangesAsync();

            LogKaydet("Aidat Gideri Silindi", $"'{gider.Kantin?.Ad}' için {gider.AyYil} dönemindeki {gider.Miktar:N0} TL gider silindi. Açıklama: {gider.Aciklama}");

            TempData["Hata"] = "Gider kaydı silindi.";
            return RedirectToAction("AidatTakip", new { ayYil = gider.AyYil, kantinId = gider.KantinId });
        }

        [HttpPost]
        public async Task<IActionResult> AidatSil(int id, string ayYil)
        {
            var aidat = await _context.Aidatlar
                .Include(a => a.Kullanici)
                .Include(a => a.Kantin)
                .FirstOrDefaultAsync(a => a.Id == id && !a.SilindiMi);

            if (aidat != null)
            {
                var username = User.Identity?.Name;
                var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
                if (user == null || !await KullaniciTahsilYetkisiVar(user, aidat.KantinId, aidat.KullaniciId)) return Unauthorized();

                aidat.SilindiMi = true;
                await _context.SaveChangesAsync();

                LogKaydet("⚠️ Tahsilat İptali", $"'{aidat.Kantin?.Ad}' kasasına ait '{aidat.Kullanici?.KullaniciAdi}' adlı personelin {aidat.Miktar:N0} TL'lik ödemesi sistemden silindi/iptal edildi. (Dönem: {aidat.AyYil})");

                TempData["Hata"] = "Aidat tahsilatı iptal edildi.";
            }
            else
            {
                TempData["Hata"] = "Aidat tahsilatı bulunamadı veya zaten iptal edilmiş.";
            }

            return RedirectToAction("AidatTakip", new { ayYil = ayYil, kantinId = aidat?.KantinId });
        }

        // --- YENİ EKLENEN: PERSONEL GEÇMİŞ ÖDEMELERİNİ GÖRME METODU ---
        public async Task<IActionResult> AidatGecmisi()
        {
            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            if (user == null) return RedirectToAction("Login", "Account");

            var gecmisOdemeler = await _context.Aidatlar
                .Include(a => a.Kantin)
                .Where(a => a.KullaniciId == user.Id && !a.SilindiMi)
                .OrderByDescending(a => a.OdemeTarihi)
                .ToListAsync();

            return View(gecmisOdemeler);
        }
        // --------------------------------------------------------------

        [HttpPost]
        public async Task<IActionResult> UrunEkle(string ad, int? miktar)
        {
            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            var gorevliBaglantisi = await _context.KullaniciOdalari.FirstOrDefaultAsync(ko => ko.KullaniciId == user.Id && !ko.SilindiMi);

            if (gorevliBaglantisi != null && !string.IsNullOrWhiteSpace(ad))
            {
                var trCulture = new System.Globalization.CultureInfo("tr-TR");
                string formatliAd = trCulture.TextInfo.ToTitleCase(ad.Trim().ToLower(trCulture));

                _context.Urunler.Add(new Urun { KatId = gorevliBaglantisi.KatId, Ad = formatliAd, Miktar = miktar });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UrunMiktarGuncelle(int id, int degisim)
        {
            var urun = await _context.Urunler.FindAsync(id);
            if (urun != null && urun.Miktar.HasValue)
            {
                urun.Miktar += degisim;
                if (urun.Miktar < 0) urun.Miktar = 0;
                _context.Urunler.Update(urun);
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UrunSil(int id)
        {
            var urun = await _context.Urunler.FindAsync(id);
            if (urun != null)
            {
                urun.SilindiMi = true;
                _context.Urunler.Update(urun);
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AdminReddetti(int id, string? iptalNotu)
        {
            if (!User.IsInRole("KatGorevlisi") && !User.IsInRole("SuperAdmin")) return Unauthorized();

            var talep = await _context.Talepler.FindAsync(id);
            if (talep != null && talep.Durum == "Bekliyor")
            {
                talep.Durum = "IptalEdildi";
                talep.IptalNotu = string.IsNullOrWhiteSpace(iptalNotu) ? null : iptalNotu.Trim();

                var buKatinGorevlisi = await _context.KullaniciOdalari.FirstOrDefaultAsync(ko => ko.KatId == talep.KatId && ko.Kullanici.Rol == "KatGorevlisi" && !ko.SilindiMi);
                var gorevliKatIdleri = buKatinGorevlisi != null
                    ? await _context.KullaniciOdalari.Where(ko => ko.KullaniciId == buKatinGorevlisi.KullaniciId && !ko.SilindiMi).Select(ko => ko.KatId).ToListAsync()
                    : new List<int> { talep.KatId };

                var urun = await _context.Urunler.FirstOrDefaultAsync(u => gorevliKatIdleri.Contains(u.KatId) && u.Ad == talep.IslemTuru && !u.SilindiMi);

                if (urun != null && urun.Miktar.HasValue)
                {
                    urun.Miktar += talep.Miktar;
                    _context.Urunler.Update(urun);
                }

                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group(talep.KatId.ToString()).SendAsync("SayfaGuncelle");
            }
            return Ok();
        }

        public async Task<IActionResult> Oda(int? secilenBaglantiId)
        {
            await OtomatikOnaylariKontrolEt();

            if (User.IsInRole("KatGorevlisi")) return RedirectToAction("Index");
            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            if (user == null) return RedirectToAction("Login", "Account");

            var odalari = await _context.KullaniciOdalari.Include(ko => ko.Kat).Where(ko => ko.KullaniciId == user.Id && ko.OdaNumarasi != null && !ko.SilindiMi && !ko.Kat.SilindiMi).ToListAsync();
            if (!odalari.Any()) return RedirectToAction("Login", "Account");

            int aktifBaglantiId = odalari.First().Id;

            if (secilenBaglantiId.HasValue)
            {
                aktifBaglantiId = secilenBaglantiId.Value;
                Response.Cookies.Append("AktifOdaId", aktifBaglantiId.ToString());
            }
            else if (Request.Cookies.TryGetValue("AktifOdaId", out string cookieStr) && int.TryParse(cookieStr, out int parsedId))
            {
                if (odalari.Any(o => o.Id == parsedId)) aktifBaglantiId = parsedId;
            }

            var aktifOda = odalari.First(o => o.Id == aktifBaglantiId);

            ViewBag.OdaAdi = aktifOda.OdaNumarasi;
            ViewBag.KatAdi = aktifOda.Kat?.Ad;
            ViewBag.AktifBaglantiId = aktifBaglantiId;
            ViewBag.OdaListesi = odalari;
            ViewBag.AidatYetkiliMi = (await YetkiliKantinIdleri(user)).Any();

            var buKatinGorevlisi = await _context.KullaniciOdalari
                .FirstOrDefaultAsync(ko => ko.KatId == aktifOda.KatId && ko.Kullanici.Rol == "KatGorevlisi" && !ko.SilindiMi);

            List<int> gorevliKatIdleri = new List<int> { aktifOda.KatId };

            if (buKatinGorevlisi != null)
            {
                gorevliKatIdleri = await _context.KullaniciOdalari
                    .Where(ko => ko.KullaniciId == buKatinGorevlisi.KullaniciId && !ko.SilindiMi)
                    .Select(ko => ko.KatId)
                    .ToListAsync();
            }

            var katUrunleri = await _context.Urunler
                .Where(u => gorevliKatIdleri.Contains(u.KatId) && !u.SilindiMi)
                .ToListAsync();

            ViewBag.KatUrunleri = katUrunleri;

            var aktifTalepler = await _context.Talepler
                .Where(t => t.OdaAdi == aktifOda.OdaNumarasi
                         && t.KatId == aktifOda.KatId
                         && t.SiparisVeren == user.KullaniciAdi
                         && (t.Durum == "Bekliyor" || t.Durum == "TeslimEdildi" || t.Durum == "IptalEdildi"))
                .OrderByDescending(t => t.OlusturulmaTarihi)
                .ToListAsync();

            var kantinUyeligi = await _context.KantinKullanicilari
                .Include(kk => kk.Kantin)
                .FirstOrDefaultAsync(kk => kk.KullaniciId == user.Id && !kk.SilindiMi);

            if (kantinUyeligi != null)
            {
                string buAy = DateTime.Now.ToString("yyyy-MM");
                decimal toplamOdenen = await _context.Aidatlar
                    .Where(a => a.KantinId == kantinUyeligi.KantinId && a.KullaniciId == user.Id && a.AyYil == buAy && !a.SilindiMi)
                    .SumAsync(a => a.Miktar);

                decimal aylikSabit = kantinUyeligi.Kantin.AylikSabitTutar;
                decimal kalanBorc = aylikSabit - toplamOdenen;

                ViewBag.CuzdanKantinAdi = kantinUyeligi.Kantin.Ad;
                ViewBag.CuzdanAy = DateTime.Now.ToString("MMMM yyyy");
                ViewBag.CuzdanToplamOdenen = toplamOdenen;
                ViewBag.CuzdanKalanBorc = kalanBorc;
                ViewBag.CuzdanAylikSabit = aylikSabit;
            }

            return View(aktifTalepler);
        }

        public async Task<IActionResult> Gecmis(DateTime? tarih)
        {
            await OtomatikOnaylariKontrolEt();

            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            if (user == null) return RedirectToAction("Login", "Account");

            var secilenTarih = tarih ?? DateTime.Today;
            var query = _context.Talepler.AsQueryable();

            if (User.IsInRole("KatGorevlisi"))
            {
                var gorevliKatIdleri = await _context.KullaniciOdalari
                    .Where(ko => ko.KullaniciId == user.Id && !ko.SilindiMi && !ko.Kat.SilindiMi)
                    .Select(ko => ko.KatId).ToListAsync();

                if (gorevliKatIdleri.Any()) query = query.Where(t => gorevliKatIdleri.Contains(t.KatId));
            }
            else
            {
                var odalari = await _context.KullaniciOdalari.Include(ko => ko.Kat).Where(ko => ko.KullaniciId == user.Id && ko.OdaNumarasi != null && !ko.SilindiMi && !ko.Kat.SilindiMi).ToListAsync();
                string aktifOdaNo = "";
                if (odalari.Any())
                {
                    int aktifBaglantiId = odalari.First().Id;
                    if (Request.Cookies.TryGetValue("AktifOdaId", out string cookieStr) && int.TryParse(cookieStr, out int parsedId))
                    {
                        if (odalari.Any(o => o.Id == parsedId)) aktifBaglantiId = parsedId;
                    }
                    var aktifBaglanti = odalari.FirstOrDefault(o => o.Id == aktifBaglantiId);
                    if (aktifBaglanti != null) aktifOdaNo = aktifBaglanti.OdaNumarasi;
                }
                query = query.Where(t => t.OdaAdi == aktifOdaNo && t.SiparisVeren == user.KullaniciAdi);
            }

            var bitenIsler = await query.Where(t => (t.Durum == "Tamamlandi" || t.Durum == "Tamamlanmadi" || t.Durum == "IptalEdildi") && t.OlusturulmaTarihi.Date == secilenTarih.Date)
                                        .OrderByDescending(t => t.OlusturulmaTarihi).ToListAsync();

            var kullaniciAdlari = bitenIsler.Select(t => t.OdaAdi).Distinct().ToList();
            var odaSozlugu = new Dictionary<string, string>();
            var eskiKullanicilar = await _context.Kullanicilar.Where(u => kullaniciAdlari.Contains(u.KullaniciAdi)).ToListAsync();
            var yeniBaglantilar = await _context.KullaniciOdalari.Include(ko => ko.Kullanici).Where(ko => kullaniciAdlari.Contains(ko.Kullanici.KullaniciAdi)).ToListAsync();

            foreach (var ad in kullaniciAdlari)
            {
                var yeniOda = yeniBaglantilar.FirstOrDefault(ko => ko.Kullanici.KullaniciAdi == ad && !string.IsNullOrEmpty(ko.OdaNumarasi))?.OdaNumarasi;
                if (!string.IsNullOrEmpty(yeniOda)) odaSozlugu[ad] = yeniOda;
                else
                {
                    var eskiOda = eskiKullanicilar.FirstOrDefault(u => u.KullaniciAdi == ad && !string.IsNullOrEmpty(u.OdaNumarasi))?.OdaNumarasi;
                    if (!string.IsNullOrEmpty(eskiOda)) odaSozlugu[ad] = eskiOda;
                }
            }

            ViewBag.OdaSozlugu = odaSozlugu;
            ViewBag.SecilenTarih = secilenTarih;
            return View(bitenIsler);
        }

        public class SepetItem
        {
            public string UrunAdi { get; set; }
            public int Miktar { get; set; }
            public string Not { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> SepetSiparisVer([FromBody] JsonElement data)
        {
            try
            {
                var username = User.Identity?.Name;
                var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
                if (user == null) return BadRequest("Geçersiz Kullanıcı");

                int baglantiId = data.GetProperty("baglantiId").GetInt32();
                var sepet = data.GetProperty("sepet").EnumerateArray();

                var aktifOda = await _context.KullaniciOdalari.Include(ko => ko.Kat).FirstOrDefaultAsync(ko => ko.Id == baglantiId && ko.KullaniciId == user.Id && !ko.SilindiMi && !ko.Kat.SilindiMi);
                if (aktifOda == null) return BadRequest("Oda bulunamadı");

                var buKatinGorevlisi = await _context.KullaniciOdalari.FirstOrDefaultAsync(ko => ko.KatId == aktifOda.KatId && ko.Kullanici.Rol == "KatGorevlisi" && !ko.SilindiMi);
                var gorevliKatIdleri = buKatinGorevlisi != null
                    ? await _context.KullaniciOdalari.Where(ko => ko.KullaniciId == buKatinGorevlisi.KullaniciId && !ko.SilindiMi).Select(ko => ko.KatId).ToListAsync()
                    : new List<int> { aktifOda.KatId };

                foreach (var item in sepet)
                {
                    string urunAdi = item.GetProperty("UrunAdi").GetString()?.Trim();
                    int miktar = item.GetProperty("Miktar").GetInt32();
                    string not = item.GetProperty("Not").GetString();
                    string kaydedilecekNot = string.IsNullOrEmpty(not) ? "-" : not;

                    var yeniTalep = new Talep
                    {
                        OdaAdi = aktifOda.OdaNumarasi,
                        KatId = aktifOda.KatId,
                        IslemTuru = urunAdi,
                        Aciklama = kaydedilecekNot,
                        Durum = "Bekliyor",
                        OlusturulmaTarihi = DateTime.Now,
                        SiparisVeren = user.KullaniciAdi,
                        Miktar = miktar
                    };

                    _context.Talepler.Add(yeniTalep);

                    var urun = await _context.Urunler.FirstOrDefaultAsync(u => gorevliKatIdleri.Contains(u.KatId) && u.Ad == urunAdi && !u.SilindiMi);
                    if (urun != null && urun.Miktar.HasValue)
                    {
                        urun.Miktar -= miktar;
                        if (urun.Miktar < 0) urun.Miktar = 0;
                        _context.Urunler.Update(urun);
                    }

                    await _hubContext.Clients.Group(aktifOda.KatId.ToString()).SendAsync("TalepAlindi", aktifOda.OdaNumarasi, urunAdi, kaydedilecekNot);
                }

                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest("Hata: " + ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> TalepSil(int id)
        {
            var talep = await _context.Talepler.FindAsync(id);
            if (talep != null && talep.Durum == "Bekliyor")
            {
                var buKatinGorevlisi = await _context.KullaniciOdalari.FirstOrDefaultAsync(ko => ko.KatId == talep.KatId && ko.Kullanici.Rol == "KatGorevlisi" && !ko.SilindiMi);
                var gorevliKatIdleri = buKatinGorevlisi != null
                    ? await _context.KullaniciOdalari.Where(ko => ko.KullaniciId == buKatinGorevlisi.KullaniciId && !ko.SilindiMi).Select(ko => ko.KatId).ToListAsync()
                    : new List<int> { talep.KatId };

                var urun = await _context.Urunler.FirstOrDefaultAsync(u => gorevliKatIdleri.Contains(u.KatId) && u.Ad == talep.IslemTuru && !u.SilindiMi);
                if (urun != null && urun.Miktar.HasValue)
                {
                    urun.Miktar += talep.Miktar;
                    _context.Urunler.Update(urun);
                }

                _context.Talepler.Remove(talep);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group(talep.KatId.ToString()).SendAsync("SayfaGuncelle");
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AdminTeslimEtti(int id)
        {
            if (!User.IsInRole("KatGorevlisi") && !User.IsInRole("SuperAdmin")) return Unauthorized();

            var talep = await _context.Talepler.FindAsync(id);
            if (talep != null)
            {
                talep.Durum = "TeslimEdildi";
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group(talep.KatId.ToString()).SendAsync("SayfaGuncelle");
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> HepsiniTeslimEt(string odaAdi)
        {
            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            var gorevliBaglantisi = await _context.KullaniciOdalari.Include(ko => ko.Kat).FirstOrDefaultAsync(ko => ko.KullaniciId == user.Id && !ko.SilindiMi && !ko.Kat.SilindiMi);
            if (gorevliBaglantisi == null) return BadRequest();

            var talepler = await _context.Talepler.Where(t => t.OdaAdi == odaAdi && t.KatId == gorevliBaglantisi.KatId && t.Durum == "Bekliyor").ToListAsync();

            if (talepler.Any())
            {
                foreach (var talep in talepler) talep.Durum = "TeslimEdildi";
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group(gorevliBaglantisi.KatId.ToString()).SendAsync("SayfaGuncelle");
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> OdaOnayladi(int id)
        {
            var talep = await _context.Talepler.FindAsync(id);
            if (talep != null)
            {
                talep.Durum = "Tamamlandi";
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group(talep.KatId.ToString()).SendAsync("SayfaGuncelle");
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> OdaReddetti(int id)
        {
            var talep = await _context.Talepler.FindAsync(id);
            if (talep != null)
            {
                talep.Durum = "Tamamlanmadi";
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group(talep.KatId.ToString()).SendAsync("SayfaGuncelle");
            }
            return Ok();
        }
    }
}
