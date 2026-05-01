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

        public async Task<IActionResult> Login()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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

            using var client = new HttpClient();
            var loginData = new { username = username, password = password };
            var jsonContent = new StringContent(JsonSerializer.Serialize(loginData), Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync("https://apilogin.subu.edu.tr/api/Login", jsonContent);

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

            if (!sifreDogruMu)
            {
                if (localUser.Sifre == password && localUser.Sifre != "API_LOGIN")
                {
                    sifreDogruMu = true;
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
                new Claim("AdSoyad", adSoyad),
                new Claim(ClaimTypes.Role, localUser.Rol)
            };

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

            return RedirectToAction("Oda", "Home");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}