using System.Collections.Generic;

namespace OfisServisSistemi.Models
{
    public class Kullanici
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; }
        public string Sifre { get; set; }
        public string Rol { get; set; }

        // ESKİ SİSTEM: Proje derleme hatası vermesin diye şimdilik tutuyoruz, geçiş bitince sileceğiz.
        public int? KatId { get; set; }
        public Kat Kat { get; set; }
        public string? OdaNumarasi { get; set; }

        // YENİ SİSTEM: Bir kullanıcının birden fazla odası olabilmesi için liste ekledik
        public ICollection<KullaniciOda> Odalari { get; set; } = new List<KullaniciOda>();

        public bool SilindiMi { get; set; } = false;
    }
}