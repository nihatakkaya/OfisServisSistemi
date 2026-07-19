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
        private readonly IConfiguration _configuration;

        public AdminController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private string? OkulApiSearchUserLink(string kullaniciAdi)
        {
            var searchUrl = _configuration["SchoolApi:SearchUserUrl"];
            if (string.IsNullOrWhiteSpace(searchUrl)) return null;

            var encodedUserName = Uri.EscapeDataString(kullaniciAdi);
            return searchUrl.Contains("{0}")
                ? string.Format(searchUrl, encodedUserName)
                : $"{searchUrl}?KullaniciAdi={encodedUserName}";
        }

        private static void OkulApiKullaniciAdlariniTopla(JsonElement element, List<string> sonuclar)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var deger = element.GetString();
                if (!string.IsNullOrWhiteSpace(deger))
                {
                    sonuclar.Add(deger.Trim());
                }

                return;
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    OkulApiKullaniciAdlariniTopla(item, sonuclar);
                }

                return;
            }

            if (element.ValueKind != JsonValueKind.Object) return;

            foreach (var property in element.EnumerateObject())
            {
                if ((property.Name.Equals("KullaniciAdi", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Equals("KullanıcıAdı", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Equals("UserName", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    var kullaniciAdi = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(kullaniciAdi))
                    {
                        sonuclar.Add(kullaniciAdi.Trim());
                    }
                }
                else if (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array)
                {
                    OkulApiKullaniciAdlariniTopla(property.Value, sonuclar);
                }
            }
        }

        private async Task<List<string>> OkulApiKullaniciAra(string kullaniciAdi)
        {
            if (string.IsNullOrWhiteSpace(kullaniciAdi)) return new List<string>();

            var arananKullaniciAdi = kullaniciAdi.Trim();
            var searchUrl = OkulApiSearchUserLink(arananKullaniciAdi);
            if (string.IsNullOrWhiteSpace(searchUrl)) return new List<string>();

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*");

            var response = await client.GetAsync(searchUrl);
            if (!response.IsSuccessStatusCode) return new List<string>();

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();

            try
            {
                using var document = JsonDocument.Parse(json);
                var sonuclar = new List<string>();
                OkulApiKullaniciAdlariniTopla(document.RootElement, sonuclar);

                return sonuclar
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Where(x => x.Contains(arananKullaniciAdi, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (JsonException)
            {
                return json.Contains(arananKullaniciAdi, StringComparison.OrdinalIgnoreCase)
                    ? new List<string> { arananKullaniciAdi }
                    : new List<string>();
            }
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

        private async Task AidatYoneticisiDurumunuGuncelle(int kullaniciId)
        {
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Id == kullaniciId && !u.SilindiMi);
            if (user == null) return;

            var aktifAidatYetkisiVar = await _context.AidatSorumlusuYetkileri
                .AnyAsync(y => y.KullaniciId == kullaniciId && !y.SilindiMi && !y.Kantin.SilindiMi);

            user.AidatYoneticisiMi = aktifAidatYetkisiVar;
            if (!aktifAidatYetkisiVar && user.Rol == "AidatSorumlusu")
            {
                user.Rol = string.Empty;
            }

            await _context.SaveChangesAsync();
        }

        private async Task AidatYoneticisiDurumlariniGuncelle(IEnumerable<int> kullaniciIds)
        {
            var idler = kullaniciIds.Distinct().ToList();
            if (!idler.Any()) return;

            var aktifAidatYoneticisiIds = await _context.AidatSorumlusuYetkileri
                .Where(y => idler.Contains(y.KullaniciId) && !y.SilindiMi && !y.Kantin.SilindiMi)
                .Select(y => y.KullaniciId)
                .Distinct()
                .ToListAsync();

            var aktifSet = aktifAidatYoneticisiIds.ToHashSet();
            var kullanicilar = await _context.Kullanicilar
                .Where(u => idler.Contains(u.Id) && !u.SilindiMi)
                .ToListAsync();

            foreach (var user in kullanicilar)
            {
                var aktifAidatYetkisiVar = aktifSet.Contains(user.Id);
                user.AidatYoneticisiMi = aktifAidatYetkisiVar;

                if (!aktifAidatYetkisiVar && user.Rol == "AidatSorumlusu")
                {
                    user.Rol = string.Empty;
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task AidatYetkiTutarliliginiTemizle()
        {
            var silinmisKantinIds = await _context.Kantinler
                .Where(k => k.SilindiMi)
                .Select(k => k.Id)
                .ToListAsync();

            var etkilenenKullaniciIds = new List<int>();

            if (silinmisKantinIds.Any())
            {
                var gecersizYetkiler = await _context.AidatSorumlusuYetkileri
                    .Where(y => silinmisKantinIds.Contains(y.KantinId) && !y.SilindiMi)
                    .ToListAsync();

                foreach (var yetki in gecersizYetkiler)
                {
                    yetki.SilindiMi = true;
                }

                etkilenenKullaniciIds.AddRange(gecersizYetkiler.Select(y => y.KullaniciId));
            }

            var aidatBayragiTasiyanKullaniciIds = await _context.Kullanicilar
                .Where(u => !u.SilindiMi && (u.AidatYoneticisiMi || u.Rol == "AidatSorumlusu"))
                .Select(u => u.Id)
                .ToListAsync();

            etkilenenKullaniciIds.AddRange(aidatBayragiTasiyanKullaniciIds);

            await _context.SaveChangesAsync();
            await AidatYoneticisiDurumlariniGuncelle(etkilenenKullaniciIds);
        }

        public async Task<IActionResult> Index()
        {
            await AidatYetkiTutarliliginiTemizle();

            var binalar = await _context.Binalar
                                        .Where(b => !b.SilindiMi)
                                        .Include(b => b.Katlar.Where(k => !k.SilindiMi))
                                        .ToListAsync();
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

        public async Task<IActionResult> Istatistik(DateTime? filterDate)
        {
            var bugun = DateTime.Today;
            var trCulture = new System.Globalization.CultureInfo("tr-TR");

            ViewBag.GunlukTalep = await _context.Talepler.CountAsync(t => t.OlusturulmaTarihi.Date == bugun);
            ViewBag.BekleyenTalep = await _context.Talepler.CountAsync(t => t.Durum == "Bekliyor");
            ViewBag.ToplamTamamlanan = await _context.Talepler.CountAsync(t => t.Durum == "Tamamlandi" || t.Durum == "TeslimEdildi");

            var baseQuery = _context.Talepler.AsQueryable();

            if (filterDate.HasValue)
            {
                baseQuery = baseQuery.Where(t => t.OlusturulmaTarihi.Date == filterDate.Value.Date);
                ViewBag.SecilenTarih = filterDate.Value.ToString("yyyy-MM-dd");
            }
            else
            {
                ViewBag.SecilenTarih = null;
            }

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
            kat.Kullanicilar = baglantilar.Select(ko => new Kullanici
            {
                Id = ko.Id,
                KullaniciAdi = ko.Kullanici.KullaniciAdi,
                Rol = ko.Kullanici.Rol,
                OdaNumarasi = ko.OdaNumarasi,
                AidatYoneticisiMi = ko.Kullanici.AidatYoneticisiMi
            }).ToList();
            return View(kat);
        }

        [HttpGet]
        public async Task<IActionResult> KullaniciAra(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return Json(new List<string>());

            var birlesikSonuclar = new List<string>();

            var dbSonuclar = await _context.Kullanicilar
                .Where(u => u.KullaniciAdi.Contains(q) && !u.SilindiMi)
                .Select(u => u.KullaniciAdi)
                .ToListAsync();

            birlesikSonuclar.AddRange(dbSonuclar);

            try
            {
                var apiSonuclar = await OkulApiKullaniciAra(q);
                birlesikSonuclar.AddRange(apiSonuclar);
            }
            catch
            {
            }

            var finalSonuclar = birlesikSonuclar.Distinct().OrderBy(x => x).Take(15).ToList();
            return Json(finalSonuclar);
        }

        [HttpPost]
        public async Task<IActionResult> KullaniciEkle(int katId, string kullaniciAdi, string rol, string odaNumarasi)
        {
            if (string.IsNullOrEmpty(kullaniciAdi)) return RedirectToAction("KatDetay", new { id = katId });

            kullaniciAdi = kullaniciAdi.Trim();
            odaNumarasi = string.IsNullOrEmpty(odaNumarasi) ? null : odaNumarasi.Trim();
            if (rol != "Oda" && rol != "KatGorevlisi") rol = "Oda";

            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == kullaniciAdi && !u.SilindiMi);

            if (user == null)
            {
                bool okulApisindeVarMi = false;

                try
                {
                    var apiSonuclar = await OkulApiKullaniciAra(kullaniciAdi);
                    okulApisindeVarMi = apiSonuclar.Any(x => x.Equals(kullaniciAdi, StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                }

                if (!okulApisindeVarMi)
                {
                    TempData["Hata"] = $"Sistemde veya Okulun API'sinde '{kullaniciAdi}' adında bir kişi bulunamadı!";
                    return RedirectToAction("KatDetay", new { id = katId });
                }

                user = new Kullanici
                {
                    KullaniciAdi = kullaniciAdi,
                    Sifre = "API_LOGIN",
                    Rol = rol
                };
                _context.Kullanicilar.Add(user);
            }
            else
            {
                user.Rol = rol;
            }

            await _context.SaveChangesAsync();

            bool zatenVarMi = await _context.KullaniciOdalari.AnyAsync(ko => ko.KullaniciId == user.Id && ko.KatId == katId && ko.OdaNumarasi == odaNumarasi && !ko.SilindiMi);
            if (!zatenVarMi)
            {
                _context.KullaniciOdalari.Add(new KullaniciOda { KullaniciId = user.Id, KatId = katId, OdaNumarasi = (rol == "Oda") ? odaNumarasi : null });
                await _context.SaveChangesAsync();
                LogKaydet("Kullanıcı Eklendi", $"'{kullaniciAdi}' adlı kullanıcı, Kat ID: {katId} içerisine {rol} yetkisiyle eklendi.");
            }
            else
            {
                TempData["Hata"] = "Bu kullanıcı zaten bu odada tanımlı!";
            }

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
            if (rol != "Oda" && rol != "KatGorevlisi") rol = "Oda";

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

        public async Task<IActionResult> Kantinler()
        {
            await AidatYetkiTutarliliginiTemizle();

            var kantinler = await _context.Kantinler.Where(k => !k.SilindiMi).ToListAsync();
            return View(kantinler);
        }

        [HttpPost]
        public IActionResult KantinEkle(string ad, decimal aylikSabitTutar)
        {
            if (!string.IsNullOrWhiteSpace(ad))
            {
                _context.Kantinler.Add(new Kantin { Ad = ad.Trim(), AylikSabitTutar = aylikSabitTutar });
                _context.SaveChanges();
                LogKaydet("Kantin Eklendi", $"'{ad.Trim()}' adında yeni bir kantin (Aylık: {aylikSabitTutar} TL) oluşturuldu.");
            }
            return RedirectToAction("Kantinler");
        }

        // --- YENİ EKLENEN: 1. MADDE İÇİN AİDAT GÜNCELLEME METODU ---
        [HttpPost]
        public IActionResult KantinGuncelle(int id, string ad, decimal aylikSabitTutar)
        {
            var kantin = _context.Kantinler.FirstOrDefault(k => k.Id == id && !k.SilindiMi);
            if (kantin != null && !string.IsNullOrWhiteSpace(ad))
            {
                string eskiAd = kantin.Ad;
                decimal eskiTutar = kantin.AylikSabitTutar;

                kantin.Ad = ad.Trim();
                kantin.AylikSabitTutar = aylikSabitTutar;
                _context.SaveChanges();

                LogKaydet("Kantin Güncellendi", $"'{eskiAd}' (Aylık: {eskiTutar} TL) adlı kantin, '{kantin.Ad}' (Aylık: {kantin.AylikSabitTutar} TL) olarak değiştirildi.");
            }
            return RedirectToAction("Kantinler");
        }
        // -----------------------------------------------------------

        [HttpPost]
        public async Task<IActionResult> KantinSil(int id)
        {
            var kantin = await _context.Kantinler.FirstOrDefaultAsync(k => k.Id == id);
            if (kantin != null)
            {
                kantin.SilindiMi = true;
                var baglantilar = await _context.KantinKullanicilari.Where(kk => kk.KantinId == id).ToListAsync();
                foreach (var b in baglantilar) b.SilindiMi = true;

                var aidatYetkileri = await _context.AidatSorumlusuYetkileri.Where(y => y.KantinId == id).ToListAsync();
                var etkilenenKullaniciIds = aidatYetkileri.Select(y => y.KullaniciId).Distinct().ToList();
                foreach (var yetki in aidatYetkileri) yetki.SilindiMi = true;

                await _context.SaveChangesAsync();
                await AidatYoneticisiDurumlariniGuncelle(etkilenenKullaniciIds);
                LogKaydet("Kantin Silindi", $"'{kantin.Ad}' adlı kantin silindi.");
            }
            return RedirectToAction("Kantinler");
        }

        public IActionResult KantinDetay(int id)
        {
            var kantin = _context.Kantinler
                .Include(k => k.Uyeler.Where(u => !u.SilindiMi))
                    .ThenInclude(u => u.Kullanici)
                .FirstOrDefault(k => k.Id == id && !k.SilindiMi);

            if (kantin == null) return RedirectToAction("Kantinler");

            ViewBag.Binalar = _context.Binalar
                .Include(b => b.Katlar.Where(k => !k.SilindiMi))
                .Where(b => !b.SilindiMi)
                .ToList();

            ViewBag.AidatYetkileri = _context.AidatSorumlusuYetkileri
                .Include(y => y.Kullanici)
                .Include(y => y.Bina)
                .Include(y => y.Kat)
                .Where(y => y.KantinId == id && !y.SilindiMi)
                .OrderBy(y => y.Kullanici.KullaniciAdi)
                .ToList();

            var uyeIds = kantin.Uyeler.Select(u => u.KullaniciId).Distinct().ToList();
            var odaBaglantilari = _context.KullaniciOdalari
                .Include(ko => ko.Kat)
                    .ThenInclude(k => k.Bina)
                .Where(ko => uyeIds.Contains(ko.KullaniciId)
                             && !ko.SilindiMi
                             && !ko.Kat.SilindiMi
                             && !ko.Kat.Bina.SilindiMi)
                .ToList();

            var uyeGruplari = new List<KantinUyeGrup>();

            foreach (var uyelik in kantin.Uyeler.OrderBy(u => u.Kullanici.KullaniciAdi))
            {
                var kullaniciOdalari = odaBaglantilari
                    .Where(ko => ko.KullaniciId == uyelik.KullaniciId)
                    .OrderBy(ko => ko.Kat.Bina.Ad)
                    .ThenBy(ko => ko.Kat.Ad)
                    .ThenBy(ko => ko.OdaNumarasi)
                    .ToList();

                if (!kullaniciOdalari.Any())
                {
                    var grup = uyeGruplari.FirstOrDefault(g => g.OdaBilgisiYok);
                    if (grup == null)
                    {
                        grup = new KantinUyeGrup
                        {
                            BinaAdi = "Oda Bilgisi Olmayanlar",
                            KatAdi = "Kayıtlı kat bulunamadı",
                            OdaBilgisiYok = true
                        };
                        uyeGruplari.Add(grup);
                    }

                    grup.Uyeler.Add(new KantinUyeSatir
                    {
                        UyelikId = uyelik.Id,
                        KullaniciAdi = uyelik.Kullanici.KullaniciAdi,
                        Rol = uyelik.Kullanici.Rol,
                        OdaNumarasi = null,
                        AidatYoneticisiMi = uyelik.Kullanici.AidatYoneticisiMi
                    });
                    continue;
                }

                foreach (var oda in kullaniciOdalari)
                {
                    var grup = uyeGruplari.FirstOrDefault(g => !g.OdaBilgisiYok
                                                            && g.BinaAdi == oda.Kat.Bina.Ad
                                                            && g.KatAdi == oda.Kat.Ad);

                    if (grup == null)
                    {
                        grup = new KantinUyeGrup
                        {
                            BinaId = oda.Kat.BinaId,
                            KatId = oda.KatId,
                            BinaAdi = oda.Kat.Bina.Ad,
                            KatAdi = oda.Kat.Ad
                        };
                        uyeGruplari.Add(grup);
                    }

                    grup.Uyeler.Add(new KantinUyeSatir
                    {
                        UyelikId = uyelik.Id,
                        KullaniciAdi = uyelik.Kullanici.KullaniciAdi,
                        Rol = uyelik.Kullanici.Rol,
                        OdaNumarasi = oda.OdaNumarasi,
                        AidatYoneticisiMi = uyelik.Kullanici.AidatYoneticisiMi
                    });
                }
            }

            ViewBag.UyeGruplari = uyeGruplari
                .OrderBy(g => g.OdaBilgisiYok)
                .ThenBy(g => g.BinaAdi)
                .ThenBy(g => g.KatAdi)
                .ToList();

            return View(kantin);
        }

        [HttpPost]
        public async Task<IActionResult> AidatSorumlusuYetkiEkle(int kantinId, string kullaniciAdi, List<string> kapsamlar)
        {
            kapsamlar = kapsamlar?
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct()
                .ToList() ?? new List<string>();

            if (string.IsNullOrWhiteSpace(kullaniciAdi) || !kapsamlar.Any())
            {
                TempData["Hata"] = "Kullanıcı adı ve en az bir yetki kapsamı zorunludur.";
                return RedirectToAction("KantinDetay", new { id = kantinId });
            }

            kullaniciAdi = kullaniciAdi.Trim();
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == kullaniciAdi && !u.SilindiMi);
            var kantin = await _context.Kantinler.FirstOrDefaultAsync(k => k.Id == kantinId && !k.SilindiMi);

            if (kantin == null) return RedirectToAction("Kantinler");
            if (user == null)
            {
                TempData["Hata"] = $"'{kullaniciAdi}' adlı kullanıcı veritabanında bulunamadı.";
                return RedirectToAction("KantinDetay", new { id = kantinId });
            }

            var secilenKapsamlar = new List<(int? BinaId, int? KatId, string KapsamMetni)>();

            foreach (var kapsam in kapsamlar)
            {
                var parcalar = kapsam.Split(':', 2);
                if (parcalar.Length != 2 || !int.TryParse(parcalar[1], out int kapsamId))
                {
                    TempData["Hata"] = "Geçersiz yetki kapsamı seçildi.";
                    return RedirectToAction("KantinDetay", new { id = kantinId });
                }

                if (parcalar[0] == "bina")
                {
                    var bina = await _context.Binalar.FirstOrDefaultAsync(b => b.Id == kapsamId && !b.SilindiMi);
                    if (bina == null)
                    {
                        TempData["Hata"] = "Seçilen bina bulunamadı.";
                        return RedirectToAction("KantinDetay", new { id = kantinId });
                    }

                    secilenKapsamlar.Add((bina.Id, null, bina.Ad));
                }
                else if (parcalar[0] == "kat")
                {
                    var kat = await _context.Katlar.Include(k => k.Bina).FirstOrDefaultAsync(k => k.Id == kapsamId && !k.SilindiMi);
                    if (kat == null)
                    {
                        TempData["Hata"] = "Seçilen kat bulunamadı.";
                        return RedirectToAction("KantinDetay", new { id = kantinId });
                    }

                    secilenKapsamlar.Add((null, kat.Id, $"{kat.Bina.Ad} / {kat.Ad}"));
                }
                else
                {
                    TempData["Hata"] = "Geçersiz yetki kapsamı seçildi.";
                    return RedirectToAction("KantinDetay", new { id = kantinId });
                }
            }

            foreach (var secilenKapsam in secilenKapsamlar)
            {
                var mevcutYetki = await _context.AidatSorumlusuYetkileri
                    .FirstOrDefaultAsync(y => y.KantinId == kantinId
                                           && y.KullaniciId == user.Id
                                           && y.BinaId == secilenKapsam.BinaId
                                           && y.KatId == secilenKapsam.KatId);

                if (mevcutYetki == null)
                {
                    _context.AidatSorumlusuYetkileri.Add(new AidatSorumlusuYetki
                    {
                        KantinId = kantinId,
                        KullaniciId = user.Id,
                        BinaId = secilenKapsam.BinaId,
                        KatId = secilenKapsam.KatId
                    });
                }
                else
                {
                    mevcutYetki.SilindiMi = false;
                }
            }

            user.AidatYoneticisiMi = true;
            if (string.IsNullOrWhiteSpace(user.Rol)) user.Rol = "AidatSorumlusu";

            await _context.SaveChangesAsync();

            var kapsamOzeti = string.Join(", ", secilenKapsamlar.Select(k => k.KapsamMetni));
            LogKaydet("Aidat Sorumlusu Yetkisi", $"'{user.KullaniciAdi}' kullanıcısı '{kantin.Ad}' için '{kapsamOzeti}' kapsamında aidat sorumlusu yapıldı.");

            return RedirectToAction("KantinDetay", new { id = kantinId });
        }

        [HttpPost]
        public async Task<IActionResult> AidatSorumlusuYetkiSil(int id)
        {
            var yetki = await _context.AidatSorumlusuYetkileri
                .Include(y => y.Kullanici)
                .Include(y => y.Kantin)
                .FirstOrDefaultAsync(y => y.Id == id);

            if (yetki == null) return RedirectToAction("Kantinler");

            yetki.SilindiMi = true;
            await _context.SaveChangesAsync();
            await AidatYoneticisiDurumunuGuncelle(yetki.KullaniciId);

            LogKaydet("Aidat Sorumlusu Yetkisi Kaldırıldı", $"'{yetki.Kullanici.KullaniciAdi}' kullanıcısının '{yetki.Kantin.Ad}' aidat sorumluluğu kaldırıldı.");

            return RedirectToAction("KantinDetay", new { id = yetki.KantinId });
        }

        [HttpPost]
        public async Task<IActionResult> KantinKullaniciTopluEkle(int kantinId, int katId)
        {
            var kattakiKullanicilar = await _context.KullaniciOdalari
                .Where(ko => ko.KatId == katId && !ko.SilindiMi)
                .Select(ko => ko.KullaniciId)
                .Distinct()
                .ToListAsync();

            var kantin = await _context.Kantinler.FirstOrDefaultAsync(k => k.Id == kantinId);
            var kat = await _context.Katlar.Include(k => k.Bina).FirstOrDefaultAsync(k => k.Id == katId);

            if (kantin == null || kat == null) return RedirectToAction("Kantinler");

            int eklenenSayisi = 0;

            foreach (var kId in kattakiKullanicilar)
            {
                var mevcutUyelikler = await _context.KantinKullanicilari
                    .Where(kk => kk.KantinId == kantinId && kk.KullaniciId == kId)
                    .ToListAsync();

                if (!mevcutUyelikler.Any())
                {
                    _context.KantinKullanicilari.Add(new KantinKullanici
                    {
                        KantinId = kantinId,
                        KullaniciId = kId
                    });
                    eklenenSayisi++;
                }
                else if (!mevcutUyelikler.Any(kk => !kk.SilindiMi))
                {
                    mevcutUyelikler.First().SilindiMi = false;
                    eklenenSayisi++;
                }
            }

            if (eklenenSayisi > 0)
            {
                await _context.SaveChangesAsync();
                LogKaydet("Kantine Toplu Ekleme", $"'{kat.Bina.Ad} - {kat.Ad}' personelleri ({eklenenSayisi} kişi) '{kantin.Ad}' kantinine bağlandı.");
                TempData["Basari"] = $"{eklenenSayisi} personel kantine başarıyla eklendi.";
            }
            else
            {
                TempData["Hata"] = "Bu kattaki personeller zaten kantine ekli veya katta personel bulunmuyor.";
            }

            return RedirectToAction("KantinDetay", new { id = kantinId });
        }

        [HttpPost]
        public async Task<IActionResult> KantinKatKullaniciEkle(int kantinId, int katId, string kullaniciAdi)
        {
            if (string.IsNullOrWhiteSpace(kullaniciAdi))
            {
                TempData["Hata"] = "Kullanıcı adı boş olamaz.";
                return RedirectToAction("KantinDetay", new { id = kantinId });
            }

            kullaniciAdi = kullaniciAdi.Trim();

            var kantin = await _context.Kantinler.FirstOrDefaultAsync(k => k.Id == kantinId && !k.SilindiMi);
            var kat = await _context.Katlar
                .Include(k => k.Bina)
                .FirstOrDefaultAsync(k => k.Id == katId && !k.SilindiMi && !k.Bina.SilindiMi);
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == kullaniciAdi && !u.SilindiMi);

            if (kantin == null || kat == null) return RedirectToAction("Kantinler");

            if (user == null)
            {
                TempData["Hata"] = $"'{kullaniciAdi}' adlı kullanıcı veritabanında bulunamadı.";
                return RedirectToAction("KantinDetay", new { id = kantinId });
            }

            var katBaglantilari = await _context.KullaniciOdalari
                .Where(ko => ko.KullaniciId == user.Id && ko.KatId == katId)
                .ToListAsync();

            bool katBaglantisiEklendi = false;

            if (!katBaglantilari.Any())
            {
                _context.KullaniciOdalari.Add(new KullaniciOda
                {
                    KullaniciId = user.Id,
                    KatId = katId,
                    OdaNumarasi = null
                });
                katBaglantisiEklendi = true;
            }
            else if (!katBaglantilari.Any(ko => !ko.SilindiMi))
            {
                katBaglantilari.First().SilindiMi = false;
                katBaglantisiEklendi = true;
            }

            var mevcutUyelikler = await _context.KantinKullanicilari
                .Where(kk => kk.KantinId == kantinId && kk.KullaniciId == user.Id)
                .ToListAsync();

            if (!mevcutUyelikler.Any())
            {
                _context.KantinKullanicilari.Add(new KantinKullanici
                {
                    KantinId = kantinId,
                    KullaniciId = user.Id
                });

                await _context.SaveChangesAsync();
                LogKaydet("Kantine Tekil Ekleme", $"'{user.KullaniciAdi}' kullanıcısı '{kat.Bina.Ad} - {kat.Ad}' katı üzerinden '{kantin.Ad}' kantinine eklendi.");
                TempData["Basari"] = $"'{user.KullaniciAdi}' kullanıcısı {kat.Bina.Ad} - {kat.Ad} katına ve kantine eklendi.";
            }
            else if (!mevcutUyelikler.Any(kk => !kk.SilindiMi))
            {
                mevcutUyelikler.First().SilindiMi = false;

                await _context.SaveChangesAsync();
                LogKaydet("Kantine Tekil Ekleme", $"'{user.KullaniciAdi}' kullanıcısının '{kantin.Ad}' kantin üyeliği tekrar aktif edildi.");
                TempData["Basari"] = $"'{user.KullaniciAdi}' kullanıcısı kantine tekrar eklendi.";
            }
            else
            {
                await _context.SaveChangesAsync();
                if (katBaglantisiEklendi)
                {
                    LogKaydet("Kantin Kat Bağlantısı", $"'{user.KullaniciAdi}' kullanıcısı zaten '{kantin.Ad}' kantinindeydi, ayrıca '{kat.Bina.Ad} - {kat.Ad}' katına bağlandı.");
                    TempData["Basari"] = $"'{user.KullaniciAdi}' zaten kantindeydi; {kat.Bina.Ad} - {kat.Ad} katına da eklendi.";
                }
                else
                {
                    TempData["Hata"] = $"'{user.KullaniciAdi}' kullanıcısı zaten bu kantine ve bu kata ekli.";
                }
            }

            return RedirectToAction("KantinDetay", new { id = kantinId });
        }

        [HttpPost]
        public async Task<IActionResult> KantinKatKullanicilariniSil(int kantinId, int katId)
        {
            var kantin = await _context.Kantinler.FirstOrDefaultAsync(k => k.Id == kantinId && !k.SilindiMi);
            var kat = await _context.Katlar
                .Include(k => k.Bina)
                .FirstOrDefaultAsync(k => k.Id == katId && !k.SilindiMi && !k.Bina.SilindiMi);

            if (kantin == null || kat == null) return RedirectToAction("Kantinler");

            var kattakiKullaniciIdleri = await _context.KullaniciOdalari
                .Where(ko => ko.KatId == katId && !ko.SilindiMi)
                .Select(ko => ko.KullaniciId)
                .Distinct()
                .ToListAsync();

            var silinecekUyelikler = await _context.KantinKullanicilari
                .Where(kk => kk.KantinId == kantinId
                             && kattakiKullaniciIdleri.Contains(kk.KullaniciId)
                             && !kk.SilindiMi)
                .ToListAsync();

            foreach (var uyelik in silinecekUyelikler)
            {
                uyelik.SilindiMi = true;
            }

            if (silinecekUyelikler.Any())
            {
                await _context.SaveChangesAsync();
                LogKaydet("Kantinden Toplu Çıkarma", $"'{kat.Bina.Ad} - {kat.Ad}' personelleri ({silinecekUyelikler.Count} kişi) '{kantin.Ad}' kantininden çıkarıldı.");
                TempData["Basari"] = $"{kat.Bina.Ad} - {kat.Ad} kapsamındaki {silinecekUyelikler.Count} personel kantinden çıkarıldı.";
            }
            else
            {
                TempData["Hata"] = "Bu katta kantine bağlı aktif personel bulunamadı.";
            }

            return RedirectToAction("KantinDetay", new { id = kantinId });
        }

        [HttpPost]
        public IActionResult KantinKullaniciSil(int id)
        {
            var uyelik = _context.KantinKullanicilari.FirstOrDefault(kk => kk.Id == id);
            if (uyelik != null)
            {
                uyelik.SilindiMi = true;
                _context.SaveChanges();
            }
            return RedirectToAction("KantinDetay", new { id = uyelik?.KantinId });
        }

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

            string fileName = filterDate.HasValue
                ? $"Tuketim_Raporu_{filterDate.Value:yyyy_MM_dd}.csv"
                : $"Tuketim_Raporu_TumZamanlar_{DateTime.Now:yyyy_MM_dd}.csv";

            return File(bytes, "text/csv", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> AidatExcelAktar(string ayYil)
        {
            var query = _context.Aidatlar
                .Include(a => a.Kullanici)
                .Include(a => a.Kantin)
                .Where(a => !a.SilindiMi);

            if (!string.IsNullOrEmpty(ayYil))
            {
                query = query.Where(a => a.AyYil == ayYil);
            }

            var aidatlar = await query.OrderByDescending(a => a.OdemeTarihi).ToListAsync();
            var builder = new System.Text.StringBuilder();

            builder.AppendLine("Kantin Adi;Personel / Oda;Odenen Tutar (TL);Ait Oldugu Ay;Odeme Tarihi;Aciklama");

            foreach (var a in aidatlar)
            {
                builder.AppendLine($"{a.Kantin?.Ad};{a.Kullanici?.KullaniciAdi};{a.Miktar};{a.AyYil};{a.OdemeTarihi.ToString("dd.MM.yyyy HH:mm")};{a.Aciklama}");
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString())).ToArray();

            string fileName = string.IsNullOrEmpty(ayYil)
                ? $"Aidat_Raporu_TumZamanlar_{DateTime.Now:yyyy_MM_dd}.csv"
                : $"Aidat_Raporu_{ayYil}.csv";

            return File(bytes, "text/csv", fileName);
        }
    }
}
