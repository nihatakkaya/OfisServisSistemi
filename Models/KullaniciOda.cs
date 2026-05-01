namespace OfisServisSistemi.Models
{
    public class KullaniciOda
    {
        public int Id { get; set; }

        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; }

        public int KatId { get; set; }
        public Kat Kat { get; set; }

        public string? OdaNumarasi { get; set; }

        public bool SilindiMi { get; set; } = false;
    }
}