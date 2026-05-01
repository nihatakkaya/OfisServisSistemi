using System;

namespace OfisServisSistemi.Models
{
    public class SistemLog
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; }
        public string IslemTuru { get; set; }
        public string Detay { get; set; }
        public DateTime Tarih { get; set; } = DateTime.Now;
    }
}