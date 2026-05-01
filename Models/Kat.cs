using System.Collections.Generic;

namespace OfisServisSistemi.Models
{
    public class Kat
    {
        public int Id { get; set; }
        public string Ad { get; set; }
        public int BinaId { get; set; }
        public bool SilindiMi { get; set; }

        // YENİ EKLENEN ÖZELLİKLER
        public bool MolaVarMi { get; set; }
        public string Duyuru { get; set; }

        public virtual Bina Bina { get; set; }
        public virtual ICollection<Kullanici> Kullanicilar { get; set; }
    }
}