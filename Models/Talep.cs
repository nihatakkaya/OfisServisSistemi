namespace OfisServisSistemi.Models
{
    public class Talep
    {
        public int Id { get; set; }
        public string OdaAdi { get; set; }
        public int KatId { get; set; }
        public string IslemTuru { get; set; }
        public string Aciklama { get; set; }
        public string Durum { get; set; }
        public DateTime OlusturulmaTarihi { get; set; }
        public string? SiparisVeren { get; set; }

        // YENİ EKLENEN ÖZELLİKLER
        public int Miktar { get; set; } = 1; // 3. madde (Sepet) için altyapı
        public string? IptalNotu { get; set; } // Görevlinin reddederken girdiği not
    }
}