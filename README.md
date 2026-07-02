# ☕ Kurumsal Ofis Servis, Kantin ve Aidat Yönetim Sistemi (OYS)

[![.NET Version](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Database](https://img.shields.io/badge/Database-SQL_Server-red.svg)](https://www.microsoft.com/en-us/sql-server/)
[![Realtime](https://img.shields.io/badge/Realtime-SignalR-orange.svg)](https://learn.microsoft.com/en-us/aspnet/core/signalr/)
[![PWA](https://img.shields.io/badge/PWA-Supported-brightgreen.svg)]()
[![Reporting](https://img.shields.io/badge/Reporting-Excel%20%2F%20CSV-success.svg)]()

Kurumsal yapılar, üniversiteler ve büyük ofisler için geliştirilmiş; gerçek zamanlı sipariş yönetimi, kantin/çay ocağı operasyonları, stok takibi, aidat tahsilatı, harcama yönetimi ve finansal raporlama modüllerini barındıran kapsamlı bir **Ofis Servis, Kantin ve Aidat Yönetim Sistemi**’dir.

Sistem; geleneksel diyafon, telefon veya anlık mesajlaşma uygulamaları üzerinden yürütülen dağınık sipariş süreçlerini dijitalleştirir. Mutfak, kat görevlileri, kurum personeli, aidat sorumluları ve yöneticiler arasındaki iş akışını tek merkezden, anlık, takip edilebilir ve raporlanabilir şekilde yönetir.

---

## 🚀 Projenin Amacı

Bu proje, kurum içi çay ocağı ve kantin süreçlerinde yaşanan şu problemlere çözüm üretmek amacıyla geliştirilmiştir:

- Siparişlerin sözlü, telefonla veya mesajlaşma uygulamalarıyla dağınık şekilde alınması
- Sipariş durumlarının anlık takip edilememesi
- Kat görevlisi, mutfak ve personel arasındaki iletişim kopukluğu
- Aidat tahsilatlarının manuel tutulması
- Kısmi ödeme, kalan borç ve aylık bakiye takibinin zorlaşması
- Kantin giderlerinin şeffaf şekilde raporlanamaması
- Yönetici tarafında tüketim, tahsilat ve harcama analizlerinin manuel yapılması

OYS, tüm bu süreçleri dijitalleştirerek hem operasyonel hızı artırır hem de kurum içi servis süreçlerini kayıt altına alır.

---

## ✨ Projede Geliştirilen Temel Özellikler

### 1. Gerçek Zamanlı Sipariş Yönetimi

Personel tarafından verilen siparişler, sayfa yenilenmesine gerek kalmadan ilgili kat görevlisinin ekranına anlık olarak düşer.

- SignalR ile gerçek zamanlı sipariş bildirimi
- Sesli uyarı desteği
- Sipariş durumlarının çift taraflı senkronizasyonu
- Hazırlanıyor, Teslim Edildi, İptal Edildi ve Tamamlandı durum yönetimi
- Personel ekranında anlık sipariş takibi

---

### 2. Akıllı Otomatik Onay Motoru

Kat görevlisinin teslim ettiği bir sipariş, kullanıcı tarafından yoğunluk nedeniyle “Teslim Aldım” olarak onaylanmazsa sistemde askıda kalmaz.

Arka planda çalışan kontrol mekanizması, belirlenen süre boyunca işlem görmeyen “Teslim Edildi” statüsündeki siparişleri otomatik olarak “Tamamlandı” durumuna geçirir.

Bu sayede:

- Eski siparişler ekranda birikmez
- Görevli ekranı temiz kalır
- Sipariş akışı manuel müdahaleye gerek kalmadan devam eder

---

### 3. Harici API Entegreli Canlı Personel Arama

Sisteme yeni personel veya oda tanımlanırken akıllı arama motoru devreye girer.

Kullanıcı adı yazılırken sistem:

- Yerel veritabanını kontrol eder
- Kurumun harici API servisini sorgular
- Canlı öneriler sunar
- Hatalı veya hayalet kullanıcı girişlerini backend seviyesinde engeller

Bu özellik sayesinde personel tanımlama işlemleri daha hızlı ve güvenli hale gelir.

---

### 4. Esnek Mutfak, Kat ve Stok Yönetimi

Kat görevlileri kendi ekranları üzerinden servis süreçlerini yönetebilir.

Öne çıkan özellikler:

- Ürün stoklarını dinamik olarak yönetme
- Ürünleri aktif/pasif duruma alma
- Kat veya mutfak durumunu geçici olarak siparişe kapatma
- Kat geneline duyuru yayınlama
- Akordeon liste yapısıyla düzenli ürün yönetimi
- Görevli bazlı operasyon takibi

Örneğin “Çay makinesi arızalıdır” gibi bir duyuru, ilgili kata anlık olarak gösterilebilir.

---

### 5. Kantin ve Aidat Yönetimi

Projeye eklenen yeni modül ile kurum içerisindeki kantin veya çay ocağı aidat süreçleri yönetilebilir hale getirilmiştir.

Yönetici paneli üzerinden yeni kantin veya çay ocağı tanımlanabilir, aylık sabit aidat tutarı belirlenebilir ve ilgili kantine bağlı personeller yönetilebilir.

Bu modül ile:

- Kantin / çay ocağı oluşturma
- Aylık sabit aidat tutarı belirleme
- Kantine bağlı personel veya oda atama
- Kantin düzenleme ve silme işlemleri
- Finansal tahsilat raporu indirme
- Kantin bazlı aidat yönetimi

gibi işlemler yapılabilir.

![Kantin ve Aidat Yönetimi](images/kantin_ve_aidat_yonetimi.jpeg)

---

### 6. Aidat Sorumlusu Yetkilendirme

Yeni geliştirilen yetkilendirme ekranı sayesinde belirli kullanıcılar aidat sorumlusu olarak atanabilir.

Aidat sorumlusu, kendisine verilen kapsam dahilinde aidat tahsilatı ve gider işlemlerini yönetebilir.

Yetkilendirme sistemi şu yapıları destekler:

- Kullanıcı adına göre aidat sorumlusu atama
- Bina bazlı yetkilendirme
- Kat bazlı yetkilendirme
- Tüm bina için yetki verme
- Mevcut yetkiyi kaldırma
- Sorumluluk kapsamını görüntüleme

Bu sayede her aidat sorumlusu yalnızca yetkili olduğu bina veya kat üzerinde işlem yapabilir.

![Aidat Sorumlusu Yetkilendirme](images/aidat_sorumlusu_yetkilendirme.jpeg)

---

### 7. Hızlı ve Toplu Personel Ekleme

Aidat havuzuna personel ekleme süreci hızlandırılmıştır.

Yönetici, belirli bir bina veya kat seçerek o kapsamdaki tüm personelleri tek işlemle kantin aidat havuzuna dahil edebilir.

Bu özellik özellikle çok personelli kurumlarda manuel ekleme yükünü azaltır.

Desteklenen işlemler:

- Bina seçerek toplu personel ekleme
- Kat seçerek toplu personel ekleme
- Kantine bağlı kişi sayısını görüntüleme
- Personel/oda bazlı listeleme
- Hızlı kapsam yönetimi

---

### 8. Aidat Havuzu ve Tahsilat Takibi

Aidat havuzu ekranı, ilgili kantin için aylık tahsilat durumunu gösterir.

Bu ekranda:

- Toplanan aidat
- Yapılan harcama
- Aylık kalan bakiye
- Personel / oda bazlı ödeme durumu
- Parçalı ödeme dökümü
- Eksik ödeme bilgisi
- Kalan tahsilat tutarı
- Tüm borcu kapatma
- Manuel tahsilat girişi

gibi işlemler takip edilebilir.

Parçalı ödeme desteği sayesinde bir personel aidat borcunu tek seferde ödemek zorunda değildir. Yapılan her ödeme ayrı ayrı kayıt altına alınır ve kalan tutar sistem tarafından otomatik hesaplanır.

![Aidat Kantin Havuzu](images/aidat_kantin_havuzu.jpeg)

---

### 9. Harcama / Gider Yönetimi

Aidat havuzu içerisine kantin giderleri girilebilir.

Örneğin:

- Çay
- Türk kahvesi
- Şeker
- Bardak
- Temizlik malzemeleri
- Diğer kantin ihtiyaçları

gibi giderler açıklama ve tutar bilgisiyle sisteme kaydedilebilir.

Gider yönetimi ekranında:

- Gider açıklaması girme
- Tutar girme
- Gider ekleme
- Giderleri listeleme
- Gideri kaydeden kullanıcıyı görüntüleme
- Gider tarihini takip etme
- Hatalı gider kaydını silme

işlemleri yapılabilir.

Bu yapı sayesinde kantinden toplanan aidatların hangi harcamalarda kullanıldığı şeffaf şekilde takip edilebilir.

---

### 10. Aidat / Harcama Raporu

Yeni raporlama ekranı ile seçilen ay için detaylı gelir-gider dökümü alınabilir.

Raporda şu bilgiler gösterilir:

- Rapor dönemi
- Toplanan toplam aidat
- Yapılan toplam harcama
- Kalan para
- Personel / oda bazlı ödeme kayıtları
- Ödeme açıklaması
- Ödeme tarihi
- Ödenen tutar
- Harcama açıklaması
- Harcamayı kaydeden kullanıcı
- Harcama tarihi
- Harcama tutarı

Bu ekran, aidat sorumluları ve yöneticiler için aylık finansal şeffaflık sağlar.

![Aidat Harcama Raporu](images/aidat_harcama_rapor.jpeg)

---

### 11. İş Zekası ve Sistem Analitiği

Kurum yöneticilerinin tüketim trendlerini ve en aktif odaları inceleyebildiği raporlama paneli mevcuttur.

Özellikler:

- Tarih bazlı filtreleme
- Bina, kat ve oda bazlı hiyerarşik tüketim raporu
- Günlük, aylık ve tarih aralıklı analiz
- Dinamik grafikler
- En aktif odalar
- En çok tüketilen ürünler
- Chart.js ile görselleştirme
- Excel / CSV çıktı alma

Filtrelenen tarih aralığına ait tüketim verileri, Türkiye Excel standartlarına uygun şekilde indirilebilir.

---

### 12. Finansal Tahsilat Raporu

Kantin ve aidat yönetimi panelinde seçilen döneme ait finansal tahsilat raporu indirilebilir.

Bu rapor ile yönetici:

- Hangi personelin ödeme yaptığını
- Hangi personelin eksik ödeme yaptığını
- Toplam tahsilatı
- Giderleri
- Kalan bakiyeyi
- Aylık aidat performansını

tek dosya üzerinden inceleyebilir.

---

### 13. PWA Desteği

Service Worker ve manifest entegrasyonu sayesinde proje, kullanıcıların telefon veya masaüstü cihazlarına yerel bir uygulama gibi kurulabilir.

Bu sayede sistem:

- Mobil cihazlarda uygulama gibi açılabilir
- Tarayıcı çubuğu olmadan kullanılabilir
- Daha hızlı erişim sağlar
- Kurum içi kullanımda pratiklik kazandırır

---

## 🛠️ Teknolojik Altyapı

### Backend

- C#
- ASP.NET Core MVC
- .NET 8.0
- Entity Framework Core
- Code First yaklaşımı
- SignalR

### Veritabanı

- SQL Server
- Entity Framework Core Migration yapısı
- İlişkisel veri modeli
- Personel, bina, kat, sipariş, ürün, kantin, aidat ve gider tabloları

### Frontend

- HTML5
- CSS3
- Vanilla JavaScript
- Responsive tasarım
- CSS değişkenleri ile tema yönetimi
- Harici CSS/JS framework bağımlılığı olmadan geliştirilmiş arayüz yapısı

### Raporlama

- Chart.js
- Excel / CSV çıktı desteği
- UTF-8 BOM uyumlu dışa aktarma
- Noktalı virgül ayracı ile Türkiye Excel formatına uyumluluk

### Uygulama Deneyimi

- Progressive Web App desteği
- Service Worker
- Manifest dosyası
- Gerçek zamanlı bildirim altyapısı

---

## 👥 Kullanıcı Rolleri

Sistem farklı kullanıcı rollerine göre çalışacak şekilde tasarlanmıştır.

### Personel / Oda Kullanıcısı

- Ürünleri görüntüler
- Sipariş oluşturur
- Sipariş durumunu takip eder
- Teslim aldığı siparişi onaylar

### Kat Görevlisi / Mutfak Kullanıcısı

- Gelen siparişleri anlık görüntüler
- Sipariş durumlarını günceller
- Stok yönetimi yapar
- Ürünleri aktif/pasif hale getirir
- Kat duyurusu yayınlar
- Servis durumunu yönetir

### Aidat Sorumlusu

- Yetkili olduğu kapsamda aidat tahsilatı yapar
- Parçalı ödeme girebilir
- Kalan borçları takip eder
- Kantin giderlerini ekler
- Aylık aidat ve harcama raporlarını görüntüler

### Yönetici / Süper Admin

- Bina ve kat yönetimi yapar
- Kantin / çay ocağı oluşturur
- Personel ve oda atamalarını yönetir
- Aidat sorumlusu yetkilendirir
- Sistem analitiklerini görüntüler
- Excel / CSV raporları indirir
- Genel sistem yönetimini gerçekleştirir

---

## 📸 Ekran Görüntüleri ve Arayüz Turu

### 1. Sisteme Giriş

Role-Based Access Control yapısı ile güvenli yetkilendirme ve kurumsal giriş ekranı.

![Giriş Ekranı](images/site_girisi.jpeg)

---

### 2. Personel / Oda Ekranı

Gerçek zamanlı sepet, ürün seçimi ve anlık sipariş durumu takip panosu.

![Oda Ekranı](images/kullanici_ekrani.jpeg)

---

### 3. Görevli Ekranı

Anlık siparişlerin düştüğü, stok ve mola yönetiminin yapıldığı kat görevlisi kontrol merkezi.

![Görevli Ekranı](images/gorevli_ekrani.jpeg)

---

### 4. Sistem ve Bina Yönetimi

Kurumun hiyerarşik altyapısının, binaların ve katların yönetildiği ana panel.

![Bina ve Kat Yönetimi](images/admin_ekrani.jpeg)

---

### 5. Personel Tanımlama

API destekli autocomplete arama motoru ile odalara ve katlara personel atama ekranı.

![Kullanıcı Yönetimi](images/admin_ekrani_2.jpeg)

---

### 6. Sistem Analitiği ve Raporlama

Tarih filtreli dinamik grafikler ve Excel çıktısı alınabilen iş zekası paneli.

![Sistem Analitiği](images/admin_ekrani_3.jpeg)

---

### 7. Kantin ve Aidat Yönetimi

Yeni kantin oluşturma, aylık aidat tutarı belirleme, kantine bağlı personelleri yönetme ve finansal rapor indirme ekranı.

![Kantin ve Aidat Yönetimi](images/kantin_ve_aidat_yonetimi.jpeg)

---

### 8. Aidat Sorumlusu Yetkilendirme

Kullanıcılara bina veya kat bazlı aidat sorumluluğu verme ekranı.

![Aidat Sorumlusu Yetkilendirme](images/aidat_sorumlusu_yetkilendirme.jpeg)

---

### 9. Aidat Havuzu ve Gider Yönetimi

Aylık toplanan aidat, yapılan harcama, kalan bakiye, parçalı tahsilat ve gider kayıtlarının yönetildiği ekran.

![Aidat Kantin Havuzu](images/aidat_kantin_havuzu.jpeg)

---

### 10. Aidat / Harcama Raporu

Seçilen aya ait toplanan aidat, yapılan harcama ve kalan bakiye bilgilerinin detaylı olarak görüntülendiği rapor ekranı.

![Aidat Harcama Raporu](images/aidat_harcama_rapor.jpeg)

---

## ⚙️ Kurulum ve Çalıştırma

### 1. Projeyi Klonlayın

```bash
git clone https://github.com/nihatakkaya/OfisServisSistemi.git
```

### 2. Proje Klasörüne Girin

```bash
cd OfisServisSistemi
```

### 3. Bağımlılıkları Yükleyin

```bash
dotnet restore
```

### 4. Veritabanı Bağlantısını Ayarlayın

`appsettings.json` dosyasında SQL Server bağlantı adresinizi düzenleyin.

Örnek:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=OfisServisSistemiDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### 5. Migration İşlemini Çalıştırın

```bash
dotnet ef database update
```

### 6. Uygulamayı Başlatın

```bash
dotnet run
```

Uygulama varsayılan olarak aşağıdaki adreslerden erişilebilir olacaktır:

```text
https://localhost:5001
http://localhost:5000
```

---

## 📊 Raporlama Özellikleri

Sistem içerisinde hem operasyonel hem de finansal raporlama özellikleri bulunmaktadır.

### Sipariş ve Tüketim Raporları

- Günlük tüketim analizi
- Tarih aralıklı sipariş raporu
- Bina / kat / oda bazlı tüketim
- En çok tüketilen ürünler
- En aktif odalar
- Grafik destekli analiz ekranları

### Aidat ve Finansal Raporlar

- Aylık toplanan aidat
- Aylık yapılan harcama
- Kalan bakiye
- Personel / oda bazlı ödeme dökümü
- Parçalı ödeme geçmişi
- Eksik ödeme takibi
- Gider kayıtları
- Excel / CSV finansal çıktı

---

## 🧩 Modül Yapısı

Proje temel olarak aşağıdaki modüllerden oluşur:

- Kimlik doğrulama ve rol bazlı yetkilendirme
- Bina ve kat yönetimi
- Personel / oda yönetimi
- Ürün ve stok yönetimi
- Sipariş yönetimi
- Gerçek zamanlı görevli ekranı
- Sistem analitiği
- Kantin ve aidat yönetimi
- Aidat sorumlusu yetkilendirme
- Aidat tahsilat havuzu
- Gider yönetimi
- Finansal raporlama
- PWA desteği

---

## 🔐 Güvenlik ve Yetkilendirme

Sistem rol bazlı erişim kontrolü prensibiyle geliştirilmiştir.

Her kullanıcı yalnızca kendi yetkisi dahilindeki ekranlara ve işlemlere erişebilir.

Örneğin:

- Personel yalnızca sipariş verebilir ve kendi siparişini takip edebilir
- Kat görevlisi yalnızca sorumlu olduğu katın siparişlerini yönetebilir
- Aidat sorumlusu yalnızca yetkili olduğu bina veya kat kapsamındaki tahsilatları yönetebilir
- Süper admin tüm sistemi yönetebilir

Bu yapı, kurum içi görev ayrımını korur ve yanlış işlem riskini azaltır.

---

## 📱 PWA Kurulumu

Proje PWA desteğine sahiptir.

Desteklenen cihazlarda uygulama:

- Masaüstüne kurulabilir
- Mobil cihaz ana ekranına eklenebilir
- Tarayıcı çubuğu olmadan çalışabilir
- Daha hızlı erişim sağlar

---

## 🧠 Öne Çıkan Teknik Noktalar

- SignalR ile gerçek zamanlı sipariş akışı
- Lazy Check algoritması ile otomatik sipariş tamamlama
- Harici API destekli canlı kullanıcı arama
- Entity Framework Core Code First mimarisi
- SQL Server ilişkisel veri modeli
- Vanilla JavaScript ile sade ve hızlı arayüz
- CSS değişkenleriyle yönetilebilir kurumsal tema
- PWA desteği
- Excel / CSV raporlama
- Parçalı aidat tahsilatı
- Bina / kat bazlı aidat sorumlusu yetkilendirme
- Aidat gelir-gider raporlama altyapısı

---

## 📌 Güncel Eklenen Özellikler

Son geliştirmelerle birlikte sisteme aşağıdaki yeni özellikler eklenmiştir:

- Kantin ve aidat yönetimi modülü
- Yeni kantin / çay ocağı oluşturma
- Aylık sabit aidat tutarı belirleme
- Aidat sorumlusu yetkilendirme
- Bina ve kat bazlı sorumluluk kapsamı
- Toplu personel ekleme
- Aidat havuzu ekranı
- Parçalı ödeme desteği
- Eksik ödeme ve kalan borç takibi
- Gider / harcama kayıt sistemi
- Aidat ve harcama raporu
- Aylık kalan bakiye takibi
- Finansal tahsilat raporu indirme

---

## 🗂️ Önerilen Görsel Dosya Yapısı

README içerisindeki görsellerin düzgün çalışması için `images` klasörünün aşağıdaki şekilde olması önerilir:

```text
images/
├── site_girisi.jpeg
├── kullanici_ekrani.jpeg
├── gorevli_ekrani.jpeg
├── admin_ekrani.jpeg
├── admin_ekrani_2.jpeg
├── admin_ekrani_3.jpeg
├── kantin_ve_aidat_yonetimi.jpeg
├── aidat_sorumlusu_yetkilendirme.jpeg
├── aidat_kantin_havuzu.jpeg
└── aidat_harcama_rapor.jpeg
```

---

## 👨‍💻 Geliştirici

Bu proje, kurum içi ofis servis süreçlerini dijitalleştirmek, sipariş ve aidat yönetimini tek merkezde toplamak ve yöneticilere raporlanabilir bir yapı sunmak amacıyla geliştirilmiştir.

---

## 📄 Lisans

Bu proje eğitim, kurumsal kullanım ve geliştirme amaçlı hazırlanmıştır. Lisans bilgisi proje sahibi tarafından belirlenebilir.
