using OfisServisSistemi.Models;

namespace OfisServisSistemi.ViewModels
{
    public class KatDurumModel
    {
        public List<Kullanici> Odalar { get; set; } = new List<Kullanici>();
        public List<Talep> AktifTalepler { get; set; } = new List<Talep>();

        // YENİ EKLENDİ: Katın çay ocağındaki güncel stok/ürün listesi
        public List<Urun> Urunler { get; set; } = new List<Urun>();
    }
}