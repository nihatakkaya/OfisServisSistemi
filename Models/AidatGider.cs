using System;

namespace OfisServisSistemi.Models
{
    public class AidatGider
    {
        public int Id { get; set; }

        public int KantinId { get; set; }
        public Kantin Kantin { get; set; }

        public string AyYil { get; set; } = string.Empty;
        public decimal Miktar { get; set; }
        public string Aciklama { get; set; } = string.Empty;
        public string KaydedenKullaniciAdi { get; set; } = string.Empty;
        public DateTime Tarih { get; set; } = DateTime.Now;
        public bool SilindiMi { get; set; } = false;
    }
}
