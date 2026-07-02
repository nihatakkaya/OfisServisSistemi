namespace OfisServisSistemi.Models
{
    public class AidatSorumlusuYetki
    {
        public int Id { get; set; }

        public int KantinId { get; set; }
        public Kantin Kantin { get; set; }

        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; }

        public int? BinaId { get; set; }
        public Bina? Bina { get; set; }

        public int? KatId { get; set; }
        public Kat? Kat { get; set; }

        public bool SilindiMi { get; set; } = false;
    }
}
