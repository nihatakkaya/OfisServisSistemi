using System.Collections.Generic;

namespace OfisServisSistemi.Models
{
    public class Kullanici
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Sifre { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public bool AidatYoneticisiMi { get; set; } = false;

        // ESKİ SİSTEM: Proje derleme hatası vermesin diye şimdilik tutuyoruz, geçiş bitince sileceğiz.
        public int? KatId { get; set; }
        public Kat? Kat { get; set; }
        public string? OdaNumarasi { get; set; }

        // YENİ SİSTEM: Bir kullanıcının birden fazla odası olabilmesi için liste ekledik
        public ICollection<KullaniciOda> Odalari { get; set; } = new List<KullaniciOda>();

        // --- YENİ EKLENEN: AİDAT VE KANTİN BAĞLANTILARI ---
        public ICollection<KantinKullanici> KantinUyelikleri { get; set; } = new List<KantinKullanici>();
        public ICollection<Aidat> OdedigiAidatlar { get; set; } = new List<Aidat>();

        public bool SilindiMi { get; set; } = false;
    }
}
