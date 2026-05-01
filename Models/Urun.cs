namespace OfisServisSistemi.Models
{
    public class Urun
    {
        public int Id { get; set; }
        public int KatId { get; set; } // Hangi katın çay ocağına ait olduğu
        public string Ad { get; set; } // Örn: Çay, Nescafe, Kola

        // Miktar (Stok) Null ise Sınırsızdır/Gramajlıdır (Toz içecekler vb.)
        public int? Miktar { get; set; }

        public bool SilindiMi { get; set; } = false;

        public virtual Kat? Kat { get; set; }
    }
}