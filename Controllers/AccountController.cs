using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfisServisSistemi.Data;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using OfisServisSistemi.Models;

namespace OfisServisSistemi.Controllers
{
    public class ApiLoginResponse
    {
        public string Ad { get; set; } = string.Empty;
        public string Soyad { get; set; } = string.Empty;
        public string KullaniciAdi { get; set; } = string.Empty;
        public bool IsAktif { get; set; }
    }

    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AccountController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return KullaniciAnaSayfasinaYonlendir();
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var localUser = await _context.Kullanicilar
                                          .FirstOrDefaultAsync(u => u.KullaniciAdi == username && !u.SilindiMi);

            string? kurucuAdmin = _configuration["SuperAdminKullaniciAdi"];

            // Ayar dosyasından gelen numara boş değilse ve giriş yapan kişi bu numaraysa onu SuperAdmin yap
            if (localUser == null && !string.IsNullOrEmpty(kurucuAdmin) && username == kurucuAdmin)
            {
                localUser = new Kullanici
                {
                    KullaniciAdi = username,
                    Sifre = "API_LOGIN",
                    Rol = "SuperAdmin"
                };
                _context.Kullanicilar.Add(localUser);
                await _context.SaveChangesAsync();
            }

            if (localUser == null)
            {
                ViewBag.Hata = "Sisteme giriş yetkiniz bulunmamaktadır. Lütfen yönetici tarafından eklendiğinizden emin olun.";
                return View();
            }

            bool sifreDogruMu = false;
            string adSoyad = username;

            if (localUser.Sifre == password && localUser.Sifre != "API_LOGIN")
            {
                sifreDogruMu = true;
            }
            else
            {
                var loginUrl = _configuration["SchoolApi:LoginUrl"];
                if (string.IsNullOrWhiteSpace(loginUrl))
                {
                    ViewBag.Hata = "Okul API ayarı bulunamadı.";
                    return View();
                }

                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(8)
                };
                var loginData = new { username = username, password = password };
                var jsonContent = new StringContent(JsonSerializer.Serialize(loginData), Encoding.UTF8, "application/json");

                try
                {
                    var response = await client.PostAsync(loginUrl, jsonContent);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var apiUser = JsonSerializer.Deserialize<ApiLoginResponse>(responseString, options);

                        if (apiUser != null && apiUser.IsAktif)
                        {
                            sifreDogruMu = true;
                            adSoyad = $"{apiUser.Ad} {apiUser.Soyad}";
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            if (!sifreDogruMu)
            {
                ViewBag.Hata = "Okul şifreniz hatalı veya API'ye ulaşılamıyor!";
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, localUser.KullaniciAdi),
                new Claim("AdSoyad", adSoyad)
            };

            if (!string.IsNullOrWhiteSpace(localUser.Rol) && localUser.Rol != "AidatSorumlusu")
            {
                claims.Add(new Claim(ClaimTypes.Role, localUser.Rol));
            }

            bool aktifAidatYetkisiVar = await _context.AidatSorumlusuYetkileri
                .AnyAsync(y => y.KullaniciId == localUser.Id && !y.SilindiMi && !y.Kantin.SilindiMi);

            if (aktifAidatYetkisiVar)
            {
                claims.Add(new Claim(ClaimTypes.Role, "AidatSorumlusu"));
            }

            var baglantilar = await _context.KullaniciOdalari.Include(ko => ko.Kat).Where(ko => ko.KullaniciId == localUser.Id && !ko.SilindiMi).ToListAsync();
            foreach (var baglanti in baglantilar)
            {
                claims.Add(new Claim("KatId", baglanti.KatId.ToString()));
                if (baglanti.Kat != null) claims.Add(new Claim("KatAdi", baglanti.Kat.Ad));
                if (!string.IsNullOrEmpty(baglanti.OdaNumarasi)) claims.Add(new Claim("OdaNumarasi", baglanti.OdaNumarasi));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTime.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            if (localUser.Rol == "SuperAdmin") return RedirectToAction("Index", "Admin");
            if (localUser.Rol == "KatGorevlisi") return RedirectToAction("Index", "Home");
            if (baglantilar.Any(b => !string.IsNullOrEmpty(b.OdaNumarasi))) return RedirectToAction("Oda", "Home");
            if (aktifAidatYetkisiVar) return RedirectToAction("AidatTakip", "Home");

            return RedirectToAction("Login", "Account");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        private IActionResult KullaniciAnaSayfasinaYonlendir()
        {
            if (User.IsInRole("SuperAdmin")) return RedirectToAction("Index", "Admin");
            if (User.IsInRole("KatGorevlisi")) return RedirectToAction("Index", "Home");
            if (User.HasClaim(c => c.Type == "OdaNumarasi")) return RedirectToAction("Oda", "Home");
            if (User.IsInRole("AidatSorumlusu")) return RedirectToAction("AidatTakip", "Home");

            return RedirectToAction("Logout", "Account");
        }
    }
}
