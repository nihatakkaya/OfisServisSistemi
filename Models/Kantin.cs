using System;
using System.Collections.Generic;

namespace OfisServisSistemi.Models
{
    public class Kantin
    {
        public int Id { get; set; }
        public string Ad { get; set; }

        // --- YENİ EKLENEN: AYLIK SABİT TUTAR ---
        public decimal AylikSabitTutar { get; set; } = 0;

        public DateTime OlusturulmaTarihi { get; set; } = DateTime.Now;
        public bool SilindiMi { get; set; } = false;

        public ICollection<KantinKullanici> Uyeler { get; set; } = new List<KantinKullanici>();
        public ICollection<Aidat> Aidatlar { get; set; } = new List<Aidat>();
    }
}