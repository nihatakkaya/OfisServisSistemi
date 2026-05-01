using System.Collections.Generic;

namespace OfisServisSistemi.Models
{
    public class Bina
    {
        public int Id { get; set; }
        public string Ad { get; set; }

        // soft delete özelliği(geçmiş bilgilerin silinmemesi için)
        public bool SilindiMi { get; set; } = false;

        public ICollection<Kat> Katlar { get; set; } = new List<Kat>();
    }
}