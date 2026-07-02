namespace OfisServisSistemi.Models
{
    public class KantinKullanici
    {
        public int Id { get; set; }

        public int KantinId { get; set; }
        public Kantin Kantin { get; set; }

        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; }

        public bool SilindiMi { get; set; } = false;
    }
}