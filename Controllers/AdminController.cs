using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfisServisSistemi.Data;
using OfisServisSistemi.Models;
using System.Text.Json;

namespace OfisServisSistemi.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
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

        public IActionResult Index()
        {
            var binalar = _context.Binalar
                                  .Where(b => !b.SilindiMi)
                                  .Include(b => b.Katlar.Where(k => !k.SilindiMi))
                                  .ToList();
            return View(binalar);
        }

        public IActionResult SistemIzKayitlari()
        {
            var loglar = _context.SistemLoglari
                                 .OrderByDescending(l => l.Tarih)
                                 .Take(500)
                                 .ToList();
            return View(loglar);
        }

        // --- GÜNCELLEME: filterDate Parametresi Eklendi ---
        public async Task<IActionResult> Istatistik(DateTime? filterDate)
        {
            var bugun = DateTime.Today;
            var trCulture = new System.Globalization.CultureInfo("tr-TR");

            // Özet kartları her zaman bugünün ve genel toplamı gösterir
            ViewBag.GunlukTalep = await _context.Talepler.CountAsync(t => t.OlusturulmaTarihi.Date == bugun);
            ViewBag.BekleyenTalep = await _context.Talepler.CountAsync(t => t.Durum == "Bekliyor");
            ViewBag.ToplamTamamlanan = await _context.Talepler.CountAsync(t => t.Durum == "Tamamlandi" || t.Durum == "TeslimEdildi");

            // Dinamik Filtreleme Mantığı
            var baseQuery = _context.Talepler.AsQueryable();

            if (filterDate.HasValue)
            {
                baseQuery = baseQuery.Where(t => t.OlusturulmaTarihi.Date == filterDate.Value.Date);
                ViewBag.SecilenTarih = filterDate.Value.ToString("yyyy-MM-dd"); // Takvime geri göndermek için
            }
            else
            {
                ViewBag.SecilenTarih = null;
            }

            // Seçilen tarihe (veya tüm zamanlara) ait verileri çek
            var tumTalepler = await baseQuery.ToListAsync();

            var islemDagilimi = tumTalepler
                .GroupBy(t => trCulture.TextInfo.ToTitleCase(t.IslemTuru.Trim().ToLower(trCulture)))
                .Select(g => new { Tur = g.Key, Sayi = g.Sum(x => x.Miktar > 0 ? x.Miktar : 1) })
                .ToList();

            ViewBag.IslemTurleri = islemDagilimi.Select(x => x.Tur).ToList();
            ViewBag.IslemSayilari = islemDagilimi.Select(x => x.Sayi).ToList();

            var topTalepler = tumTalepler
                .GroupBy(t => t.OdaAdi)
                .Select(g => new { Key = g.Key, SiparisSayisi = g.Sum(x => x.Miktar > 0 ? x.Miktar : 1) })
                .OrderByDescending(x => x.SiparisSayisi)
                .Take(5)
                .ToList();

            var anahtarlar = topTalepler.Select(t => t.Key).ToList();
            var odaSozlugu = new Dictionary<string, string>();

            var eskiKullanicilar = await _context.Kullanicilar.Where(u => anahtarlar.Contains(u.KullaniciAdi)).ToListAsync();
            var yeniBaglantilar = await _context.KullaniciOdalari.Include(ko => ko.Kullanici).Where(ko => anahtarlar.Contains(ko.Kullanici.KullaniciAdi)).ToListAsync();

            foreach (var ad in anahtarlar)
            {
                var yeniOda = yeniBaglantilar.FirstOrDefault(ko => ko.Kullanici.KullaniciAdi == ad && !string.IsNullOrEmpty(ko.OdaNumarasi))?.OdaNumarasi;
                if (!string.IsNullOrEmpty(yeniOda)) odaSozlugu[ad] = yeniOda;
                else
                {
                    var eskiOda = eskiKullanicilar.FirstOrDefault(u => u.KullaniciAdi == ad && !string.IsNullOrEmpty(u.OdaNumarasi))?.OdaNumarasi;
                    if (!string.IsNullOrEmpty(eskiOda)) odaSozlugu[ad] = eskiOda;
                }
            }

            var finalOdaAdlari = new List<string>();
            foreach (var item in topTalepler)
            {
                string temizAd = odaSozlugu.ContainsKey(item.Key) ? odaSozlugu[item.Key] : item.Key;
                finalOdaAdlari.Add("Oda " + temizAd);
            }

            ViewBag.TopOdaAdlari = finalOdaAdlari;
            ViewBag.TopOdaSiparisleri = topTalepler.Select(x => x.SiparisSayisi).ToList();

            var tamamlananTalepler = tumTalepler.Where(t => t.Durum == "Tamamlandi" || t.Durum == "TeslimEdildi").ToList();
            var binalar = await _context.Binalar.Include(b => b.Katlar).Where(b => !b.SilindiMi).ToListAsync();
            var hiyerarsiListesi = new List<object>();

            foreach (var bina in binalar)
            {
                var katListesi = new List<object>();
                int binaToplamUrun = 0;

                foreach (var kat in bina.Katlar.Where(k => !k.SilindiMi))
                {
                    var katTalepleri = tamamlananTalepler.Where(t => t.KatId == kat.Id).ToList();

                    var odalarGrup = katTalepleri
                        .GroupBy(t => t.OdaAdi)
                        .Select(g => new
                        {
                            OdaNo = g.Key,
                            Kullanici = g.First().SiparisVeren,
                            ToplamTuketim = g.Sum(x => x.Miktar > 0 ? x.Miktar : 1),
                            Urunler = g.GroupBy(u => trCulture.TextInfo.ToTitleCase(u.IslemTuru.Trim().ToLower(trCulture)))
                                       .Select(u => new { Ad = u.Key, Adet = u.Sum(x => x.Miktar > 0 ? x.Miktar : 1) })
                                       .ToList()
                        })
                        .OrderByDescending(o => o.ToplamTuketim)
                        .ToList();

                    int katToplamUrun = odalarGrup.Sum(o => o.ToplamTuketim);
                    binaToplamUrun += katToplamUrun;

                    if (katToplamUrun > 0)
                    {
                        katListesi.Add(new { KatAdi = kat.Ad, Toplam = katToplamUrun, Odalar = odalarGrup });
                    }
                }

                if (binaToplamUrun > 0)
                {
                    hiyerarsiListesi.Add(new { BinaAdi = bina.Ad, Toplam = binaToplamUrun, Katlar = katListesi });
                }
            }

            ViewBag.HiyerarsikVeri = JsonSerializer.Serialize(hiyerarsiListesi);
            return View();
        }

        [HttpPost]
        public IActionResult BinaEkle(string ad)
        {
            if (!string.IsNullOrEmpty(ad))
            {
                _context.Binalar.Add(new Bina { Ad = ad.Trim() });
                _context.SaveChanges();
                LogKaydet("Bina Eklendi", $"Sisteme '{ad.Trim()}' adında yeni bir bina eklendi.");
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult BinaSil(int id)
        {
            var bina = _context.Binalar.Include(b => b.Katlar).FirstOrDefault(b => b.Id == id);
            if (bina != null)
            {
                bina.SilindiMi = true;
                foreach (var kat in bina.Katlar)
                {
                    kat.SilindiMi = true;
                    var baglantilar = _context.KullaniciOdalari.Where(ko => ko.KatId == kat.Id).ToList();
                    foreach (var baglanti in baglantilar) baglanti.SilindiMi = true;
                }
                _context.SaveChanges();
                LogKaydet("Bina Silindi", $"Sistemden '{bina.Ad}' adlı bina silindi.");
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult BinaGuncelle(int id, string ad)
        {
            var bina = _context.Binalar.FirstOrDefault(b => b.Id == id && !b.SilindiMi);
            if (bina != null && !string.IsNullOrEmpty(ad))
            {
                string eskiAd = bina.Ad;
                bina.Ad = ad.Trim();
                _context.SaveChanges();
                LogKaydet("Bina Güncellendi", $"'{eskiAd}' adlı binanın adı '{ad.Trim()}' olarak değiştirildi.");
            }
            return RedirectToAction("Index");
        }

        public IActionResult BinaDetay(int id)
        {
            var bina = _context.Binalar.Include(b => b.Katlar.Where(k => !k.SilindiMi)).FirstOrDefault(b => b.Id == id && !b.SilindiMi);
            if (bina != null)
            {
                foreach (var kat in bina.Katlar)
                    kat.Kullanicilar = _context.KullaniciOdalari.Where(ko => ko.KatId == kat.Id && !ko.SilindiMi).Select(ko => ko.Kullanici).ToList();
            }
            if (bina == null) return RedirectToAction("Index");
            return View(bina);
        }

        [HttpPost]
        public IActionResult KatEkle(int binaId, string ad)
        {
            if (!string.IsNullOrEmpty(ad))
            {
                _context.Katlar.Add(new Kat { Ad = ad.Trim(), BinaId = binaId });
                _context.SaveChanges();
                LogKaydet("Kat Eklendi", $"Bina ID: {binaId} içerisine '{ad.Trim()}' adlı yeni bir kat eklendi.");
            }
            return RedirectToAction("BinaDetay", new { id = binaId });
        }

        [HttpPost]
        public IActionResult KatSil(int id)
        {
            var kat = _context.Katlar.FirstOrDefault(k => k.Id == id);
            if (kat != null)
            {
                kat.SilindiMi = true;
                var baglantilar = _context.KullaniciOdalari.Where(ko => ko.KatId == kat.Id).ToList();
                foreach (var baglanti in baglantilar) baglanti.SilindiMi = true;
                _context.SaveChanges();
                LogKaydet("Kat Silindi", $"Sistemden '{kat.Ad}' adlı kat silindi.");
                return RedirectToAction("BinaDetay", new { id = kat.BinaId });
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult KatGuncelle(int id, string ad)
        {
            var kat = _context.Katlar.FirstOrDefault(k => k.Id == id && !k.SilindiMi);
            if (kat != null && !string.IsNullOrEmpty(ad))
            {
                string eskiAd = kat.Ad;
                kat.Ad = ad.Trim();
                _context.SaveChanges();
                LogKaydet("Kat Güncellendi", $"'{eskiAd}' adlı katın adı '{ad.Trim()}' olarak değiştirildi.");
                return RedirectToAction("BinaDetay", new { id = kat.BinaId });
            }
            return RedirectToAction("Index");
        }

        public IActionResult KatIzle(int id)
        {
            var kat = _context.Katlar.Include(k => k.Bina).FirstOrDefault(k => k.Id == id && !k.SilindiMi);
            if (kat == null) return RedirectToAction("Index");

            var aktifOdalar = _context.KullaniciOdalari.Where(ko => ko.KatId == id && ko.OdaNumarasi != null && !ko.SilindiMi)
                .Select(ko => ko.OdaNumarasi).Distinct().Select(odaNo => new Kullanici { KullaniciAdi = odaNo, OdaNumarasi = odaNo }).ToList();

            var aktifTalepler = _context.Talepler.Where(t => t.KatId == id && (t.Durum == "Bekliyor" || t.Durum == "TeslimEdildi")).ToList();

            var model = new OfisServisSistemi.ViewModels.KatDurumModel { Odalar = aktifOdalar, AktifTalepler = aktifTalepler };
            ViewBag.KatAdi = $"{kat.Bina.Ad} - {kat.Ad}";
            return View(model);
        }

        public IActionResult KatDetay(int id)
        {
            var kat = _context.Katlar.Include(k => k.Bina).FirstOrDefault(k => k.Id == id && !k.SilindiMi);
            if (kat == null) return RedirectToAction("Index");

            var baglantilar = _context.KullaniciOdalari.Include(ko => ko.Kullanici).Where(ko => ko.KatId == id && !ko.SilindiMi).ToList();
            kat.Kullanicilar = baglantilar.Select(ko => new Kullanici { Id = ko.Id, KullaniciAdi = ko.Kullanici.KullaniciAdi, Rol = ko.Kullanici.Rol, OdaNumarasi = ko.OdaNumarasi }).ToList();
            return View(kat);
        }

        [HttpPost]
        public IActionResult KullaniciEkle(int katId, string kullaniciAdi, string rol, string odaNumarasi)
        {
            if (string.IsNullOrEmpty(kullaniciAdi)) return RedirectToAction("KatDetay", new { id = katId });

            kullaniciAdi = kullaniciAdi.Trim();
            odaNumarasi = string.IsNullOrEmpty(odaNumarasi) ? null : odaNumarasi.Trim();

            var user = _context.Kullanicilar.FirstOrDefault(u => u.KullaniciAdi == kullaniciAdi && !u.SilindiMi);
            if (user == null) { user = new Kullanici { KullaniciAdi = kullaniciAdi, Sifre = "API_LOGIN", Rol = rol }; _context.Kullanicilar.Add(user); }
            else { user.Rol = rol; }

            _context.SaveChanges();

            bool zatenVarMi = _context.KullaniciOdalari.Any(ko => ko.KullaniciId == user.Id && ko.KatId == katId && ko.OdaNumarasi == odaNumarasi && !ko.SilindiMi);
            if (!zatenVarMi)
            {
                _context.KullaniciOdalari.Add(new KullaniciOda { KullaniciId = user.Id, KatId = katId, OdaNumarasi = (rol == "Oda") ? odaNumarasi : null });
                _context.SaveChanges();
                LogKaydet("Kullanıcı Eklendi", $"'{kullaniciAdi}' adlı kullanıcı, Kat ID: {katId} içerisine {rol} yetkisiyle eklendi.");
            }
            else { TempData["Hata"] = "Bu kullanıcı zaten bu odada tanımlı!"; }

            return RedirectToAction("KatDetay", new { id = katId });
        }

        [HttpPost]
        public IActionResult KullaniciSil(int id)
        {
            var baglanti = _context.KullaniciOdalari.Include(ko => ko.Kullanici).FirstOrDefault(ko => ko.Id == id);
            if (baglanti != null)
            {
                baglanti.SilindiMi = true;
                _context.SaveChanges();
                LogKaydet("Kullanıcı Silindi", $"'{baglanti.Kullanici.KullaniciAdi}' adlı kullanıcının bağlantısı silindi.");
                return RedirectToAction("KatDetay", new { id = baglanti.KatId });
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult KullaniciGuncelle(int id, string kullaniciAdi, string rol, string odaNumarasi)
        {
            kullaniciAdi = kullaniciAdi?.Trim();
            odaNumarasi = string.IsNullOrEmpty(odaNumarasi) ? null : odaNumarasi.Trim();

            var baglanti = _context.KullaniciOdalari.Include(ko => ko.Kullanici).FirstOrDefault(ko => ko.Id == id && !ko.SilindiMi);
            if (baglanti != null)
            {
                string eskiRol = baglanti.Kullanici.Rol;
                baglanti.OdaNumarasi = (rol == "Oda") ? odaNumarasi : null;
                baglanti.Kullanici.Rol = rol;

                if (baglanti.Kullanici.KullaniciAdi != kullaniciAdi && !_context.Kullanicilar.Any(u => u.KullaniciAdi == kullaniciAdi && u.Id != baglanti.KullaniciId && !u.SilindiMi))
                    baglanti.Kullanici.KullaniciAdi = kullaniciAdi;

                _context.SaveChanges();
                LogKaydet("Kullanıcı Güncellendi", $"'{kullaniciAdi}' adlı kullanıcının yetkisi {eskiRol} -> {rol} olarak değiştirildi.");
                return RedirectToAction("KatDetay", new { id = baglanti.KatId });
            }
            return RedirectToAction("Index");
        }

        // --- GÜNCELLEME: filterDate Parametresi Eklendi ---
        [HttpGet]
        public async Task<IActionResult> ExcelAktar(DateTime? filterDate)
        {
            var query = _context.Talepler.Where(t => t.Durum == "Tamamlandi" || t.Durum == "TeslimEdildi");

            if (filterDate.HasValue)
            {
                query = query.Where(t => t.OlusturulmaTarihi.Date == filterDate.Value.Date);
            }

            var tamamlananTalepler = await query.ToListAsync();
            var binalar = await _context.Binalar.Include(b => b.Katlar).Where(b => !b.SilindiMi).ToListAsync();
            var trCulture = new System.Globalization.CultureInfo("tr-TR");

            var builder = new System.Text.StringBuilder();

            builder.AppendLine("Bina Adi;Kat Adi;Oda Numarasi;Siparis Veren;Urun Adi;Miktar;Siparis Tarihi");

            foreach (var bina in binalar)
            {
                foreach (var kat in bina.Katlar.Where(k => !k.SilindiMi))
                {
                    var katTalepleri = tamamlananTalepler.Where(t => t.KatId == kat.Id).OrderBy(t => t.OlusturulmaTarihi).ToList();

                    foreach (var talep in katTalepleri)
                    {
                        string urunAdi = trCulture.TextInfo.ToTitleCase(talep.IslemTuru.Trim().ToLower(trCulture));
                        int miktar = talep.Miktar > 0 ? talep.Miktar : 1;

                        builder.AppendLine($"{bina.Ad};{kat.Ad};{talep.OdaAdi};{talep.SiparisVeren};{urunAdi};{miktar};{talep.OlusturulmaTarihi.ToString("dd.MM.yyyy HH:mm")}");
                    }
                }
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString())).ToArray();

            // Dosya ismini tarihe göre ayarlayalım
            string fileName = filterDate.HasValue
                ? $"Tuketim_Raporu_{filterDate.Value:yyyy_MM_dd}.csv"
                : $"Tuketim_Raporu_TumZamanlar_{DateTime.Now:yyyy_MM_dd}.csv";

            return File(bytes, "text/csv", fileName);
        }
    }
}