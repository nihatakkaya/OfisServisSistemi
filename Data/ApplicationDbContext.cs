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
        public DbSet<SistemLog> SistemLoglari { get; set; }

        // --- YENİ EKLENEN: KANTİN VE AİDAT TABLOLARI ---
        public DbSet<Kantin> Kantinler { get; set; }
        public DbSet<KantinKullanici> KantinKullanicilari { get; set; }
        public DbSet<Aidat> Aidatlar { get; set; }
        public DbSet<AidatGider> AidatGiderleri { get; set; }
        public DbSet<AidatSorumlusuYetki> AidatSorumlusuYetkileri { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Aidat>()
                .Property(a => a.Miktar)
                .HasPrecision(18, 2);

            modelBuilder.Entity<AidatGider>()
                .Property(g => g.Miktar)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Kantin>()
                .Property(k => k.AylikSabitTutar)
                .HasPrecision(18, 2);
        }
    }
}
