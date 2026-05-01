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

        // --- YENÝ EKLENEN: 120 DAKÝKA OTOMATÝK ONAY SÝSTEMÝ ---
        private async Task OtomatikOnaylariKontrolEt()
        {
            // Þu andan 120 dakika (2 saat) öncesini bul
            var zamanSiniri = DateTime.Now.AddMinutes(-120);

            // Teslim edilmiþ ama 2 saattir kullanýcý tarafýndan "Aldým" diye onaylanmamýþ talepleri getir
            var unutulanTalepler = await _context.Talepler
                .Where(t => t.Durum == "TeslimEdildi" && t.OlusturulmaTarihi <= zamanSiniri)
                .ToListAsync();

            if (unutulanTalepler.Any())
            {
                foreach (var talep in unutulanTalepler)
                {
                    talep.Durum = "Tamamlandi"; // Otomatik onayla
                }
                await _context.SaveChangesAsync();
            }
        }
        // --------------------------------------------------------

        public async Task<IActionResult> Index()
        {
            // Sayfa açýldýðýnda önce arka planda unutulan sipariþleri onayla
            await OtomatikOnaylariKontrolEt();

            if (!User.IsInRole("KatGorevlisi")) return RedirectToAction("Oda");
            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            if (user == null) return RedirectToAction("Login", "Account");

            // Görevlinin sorumlu olduðu TÜM katlarý alýyoruz (Çoklu kat desteði)
            var gorevliBaglantilari = await _context.KullaniciOdalari
                .Include(ko => ko.Kat)
                .Where(ko => ko.KullaniciId == user.Id && !ko.SilindiMi && !ko.Kat.SilindiMi)
                .ToListAsync();

            if (!gorevliBaglantilari.Any()) return RedirectToAction("Login", "Account");

            var gorevliKatIdleri = gorevliBaglantilari.Select(ko => ko.KatId).ToList();

            // Aktif odalarý görevlinin tüm katlarýndan çekiyoruz
            var aktifOdalar = await _context.KullaniciOdalari
                .Where(ko => gorevliKatIdleri.Contains(ko.KatId) && ko.OdaNumarasi != null && !ko.SilindiMi && !ko.Kat.SilindiMi)
                .Select(ko => ko.OdaNumarasi)
                .Distinct()
                .Select(odaNo => new Kullanici { KullaniciAdi = odaNo, OdaNumarasi = odaNo })
                .ToListAsync();

            var gecerliOdaNumaralari = aktifOdalar.Select(o => o.OdaNumarasi).ToList();

            // Talepleri görevlinin tüm katlarýndan çekiyoruz
            var aktifTalepler = await _context.Talepler
                .Where(t => gorevliKatIdleri.Contains(t.KatId)
                         && (t.Durum == "Bekliyor" || t.Durum == "TeslimEdildi")
                         && gecerliOdaNumaralari.Contains(t.OdaAdi))
                .ToListAsync();

            // Ürünleri (Stoklarý) görevlinin tüm katlarýndan çekiyoruz
            var katUrunleri = await _context.Urunler
                .Where(u => gorevliKatIdleri.Contains(u.KatId) && !u.SilindiMi)
                .ToListAsync();

            var model = new KatDurumModel { Odalar = aktifOdalar, AktifTalepler = aktifTalepler, Urunler = katUrunleri };

            // Kat adýný dinamik olarak yazdýrýyoruz (Örn: "1. Kat, 2. Kat")
            ViewBag.KatAdi = string.Join(", ", gorevliBaglantilari.Select(gb => gb.Kat.Ad).Distinct());
            return View(model);
        }

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

                // Ortak stok iade mantýðý
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
            // Sayfa açýldýðýnda önce arka planda unutulan sipariþleri onayla
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

            // Ortak Mutfak Mantýðý. Odanýn bulunduðu kata bakan görevliyi bulup, onun TÜM stoklarýný getiriyoruz.
            var buKatinGorevlisi = await _context.KullaniciOdalari
                .FirstOrDefaultAsync(ko => ko.KatId == aktifOda.KatId && ko.Kullanici.Rol == "KatGorevlisi" && !ko.SilindiMi);

            List<int> gorevliKatIdleri = new List<int> { aktifOda.KatId }; // Yedek plan

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

            return View(aktifTalepler);
        }

        public async Task<IActionResult> Gecmis(DateTime? tarih)
        {
            // Sayfa açýldýðýnda önce arka planda unutulan sipariþleri onayla
            await OtomatikOnaylariKontrolEt();

            var username = User.Identity?.Name;
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);
            if (user == null) return RedirectToAction("Login", "Account");

            var secilenTarih = tarih ?? DateTime.Today;
            var query = _context.Talepler.AsQueryable();

            if (User.IsInRole("KatGorevlisi"))
            {
                // Görevlinin tüm katlarýnýn geçmiþi gelsin
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
                if (user == null) return BadRequest("Geçersiz Kullanýcý");

                int baglantiId = data.GetProperty("baglantiId").GetInt32();
                var sepet = data.GetProperty("sepet").EnumerateArray();

                var aktifOda = await _context.KullaniciOdalari.Include(ko => ko.Kat).FirstOrDefaultAsync(ko => ko.Id == baglantiId && ko.KullaniciId == user.Id && !ko.SilindiMi && !ko.Kat.SilindiMi);
                if (aktifOda == null) return BadRequest("Oda bulunamadý");

                // Görevli katlarý havuzunu hesapla (Stok düþmek için)
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

                    // Stok havuzdan düþülür
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
                // Ortak stok iade mantýðý
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