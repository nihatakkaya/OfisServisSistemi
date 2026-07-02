using System;

namespace OfisServisSistemi.Models
{
    public class Aidat
    {
        public int Id { get; set; }

        public int KantinId { get; set; }
        public Kantin Kantin { get; set; }

        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; }

        public decimal Miktar { get; set; } // Ödenen Para Tutarı
        public DateTime OdemeTarihi { get; set; } = DateTime.Now;
        public string AyYil { get; set; } // Hangi ay için ödendi? Örn: "2026-Haziran"
        public string Aciklama { get; set; } // Örn: "Elden verildi", "Eksik ödedi" vb.

        public bool SilindiMi { get; set; } = false;
    }
}