using Microsoft.EntityFrameworkCore;
using OfisServisSistemi.Models;

namespace OfisServisSistemi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Talep> Talepler { get; set; }
        public DbSet<Kullanici> Kullanicilar { get; set; }
        public DbSet<Bina> Binalar { get; set; }
        public DbSet<Kat> Katlar { get; set; }
        public DbSet<KullaniciOda> KullaniciOdalari { get; set; }
        public DbSet<Urun> Urunler { get; set; }

        // YENİ EKLENEN LOG TABLOSU
        public DbSet<SistemLog> SistemLoglari { get; set; }
    }
}