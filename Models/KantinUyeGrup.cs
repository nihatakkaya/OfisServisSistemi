namespace OfisServisSistemi.Models
{
    public class KantinUyeGrup
    {
        public string BinaAdi { get; set; } = string.Empty;
        public string KatAdi { get; set; } = string.Empty;
        public bool OdaBilgisiYok { get; set; }
        public List<KantinUyeSatir> Uyeler { get; set; } = new List<KantinUyeSatir>();
    }

    public class KantinUyeSatir
    {
        public int UyelikId { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string? OdaNumarasi { get; set; }
        public bool AidatYoneticisiMi { get; set; }
    }
}
