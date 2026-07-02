using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfisServisSistemi.Data;
using OfisServisSistemi.Hubs;
using OfisServisSistemi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// 1. Veritabanı Bağlantısı
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Giriş (Cookie) Ayarları
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// 3. Servisler
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddSignalR();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapHub<OfisHub>("/ofisHub");

// --- OTOMATİK VERİ YÜKLEME (SEED DATA) ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // Veritabanı yoksa oluştur
    db.Database.EnsureCreated();

    // Eğer hiç kullanıcı yoksa sistemi kur
    if (!db.Kullanicilar.Any())
    {
        // 1. Super Admin (Sistemin Sahibi)
        db.Kullanicilar.Add(new Kullanici
        {
            KullaniciAdi = "superadmin",
            Sifre = "123456",
            Rol = "SuperAdmin"
        });

        // 2. Örnek Bina: T1 Binası
        var bina = new Bina { Ad = "T1 Binası" };
        db.Binalar.Add(bina);
        db.SaveChanges(); // ID oluşsun diye kaydet

        // 3. Örnek Kat: 3. Kat (300'lüler)
        var kat = new Kat { Ad = "3. Kat", BinaId = bina.Id };
        db.Katlar.Add(kat);
        db.SaveChanges();

        // 4. Bu Katın Çaycısı (Kat Görevlisi)
        var cayci3 = new Kullanici
        {
            KullaniciAdi = "cayci3",
            Sifre = "1234",
            Rol = "KatGorevlisi",
            KatId = kat.Id
        };
        db.Kullanicilar.Add(cayci3);
        db.SaveChanges();
        db.KullaniciOdalari.Add(new KullaniciOda { KullaniciId = cayci3.Id, KatId = kat.Id });

        // 5. Bu Kattaki Odalar (301, 302, 303)
        for (int i = 301; i <= 305; i++)
        {
            var odaKullanici = new Kullanici
            {
                KullaniciAdi = i.ToString(),
                Sifre = i.ToString(),
                Rol = "Oda",
                KatId = kat.Id
            };
            db.Kullanicilar.Add(odaKullanici);
            db.SaveChanges();
            db.KullaniciOdalari.Add(new KullaniciOda
            {
                KullaniciId = odaKullanici.Id,
                KatId = kat.Id,
                OdaNumarasi = i.ToString()
            });
        }

        // 6. Örnek Kat: 2. Kat (200'lüler - Göstermelik)
        var kat2 = new Kat { Ad = "2. Kat", BinaId = bina.Id };
        db.Katlar.Add(kat2);
        db.SaveChanges();

        // 2. Katın Çaycısı
        var cayci2 = new Kullanici
        {
            KullaniciAdi = "cayci2",
            Sifre = "1234",
            Rol = "KatGorevlisi",
            KatId = kat2.Id
        };
        db.Kullanicilar.Add(cayci2);
        db.SaveChanges();
        db.KullaniciOdalari.Add(new KullaniciOda { KullaniciId = cayci2.Id, KatId = kat2.Id });

        // 2. Kattaki Odalar (201, 202)
        for (int i = 201; i <= 203; i++)
        {
            var odaKullanici = new Kullanici
            {
                KullaniciAdi = i.ToString(),
                Sifre = i.ToString(),
                Rol = "Oda",
                KatId = kat2.Id
            };
            db.Kullanicilar.Add(odaKullanici);
            db.SaveChanges();
            db.KullaniciOdalari.Add(new KullaniciOda
            {
                KullaniciId = odaKullanici.Id,
                KatId = kat2.Id,
                OdaNumarasi = i.ToString()
            });
        }

        db.SaveChanges();
    }
}
// --------------------------------------------------

app.Run();
