# StudyTime — Teknik Sistem Taraması ve Yayın Durum Raporu

Bu rapor, mevcut `StudyTimeSolution` altyapısının dosyaları, bağımlılıkları ve mimarisi üzerinden derinlemesine taranarak oluşturulmuştur. Yayına kadar (Production/Launch) eksik kalan adımların ve şu ana kadar tamamlananların tam görünümünü sunar.

---

## 🔍 1. Sistem Mimarisi ve Teknoloji Yığıtı (Tech Stack)

Proje, güncel ve modern bir **.NET 9** yapısı üzerine inşa edilmiştir.

### 🏢 Arka Plan (Backend) - `StudyTime (API)`
- **Çerçeve:** ASP.NET Core Web API (.NET 9.0)
- **Veritabanı:** Entity Framework Core (`StudyTime.Infrastructure` üzerinden yürütülüyor). Multi-tenant (veya User-based) global query filtrelerine sahip.
- **Güvenlik:** `JwtBearer` (JSON Web Token) tabanlı yetkilendirme.
- **Dokümantasyon:** Swashbuckle (Swagger) dev ortamında aktif açık.

### 📱 İstemci (Client) - `StudyTime.DesktopClient`
- **Çerçeve:** .NET MAUI + Blazor Hybrid (`net9.0-android`, `net9.0-ios`, `net9.0-maccatalyst`, `net9.0-windows10.0.19041.0`).
- **Çevrimdışı Veritabanı:** `sqlite-net-pcl` (SQLite). İnternet yokken verileri telefonda/uygulamada tutmak için kullanılıyor.
- **Özel Windows Yetenekleri:** `H.NotifyIcon.WinUI` ile sistem tepsisine (System Tray) entegrasyon sağlanmış, arka planda bildirimler atabiliyor.
- **Grafikler:** `Blazor-ApexCharts` ile İstatistikler ve Dashboard grafikleri çiziliyor.
- **Donanım Tanıma:** `System.Management` ile cihaz algısı (HWID).

### 🧩 Paylaşılan Katmanlar (Clean Architecture)
- `StudyTime.Domain`: İş kuralları, Entity tanımları.
- `StudyTime.Application`: Servisler, arayüzler, Use Case'ler.
- `StudyTime.Infrastructure`: EF Core, Repository'ler.

---

## ✅ 2. Şu Ana Kadar Yapılanlar (Completed Work)

Projenin en zor kısımları büyük ölçüde tamamlanmıştır. Uygulamanın iskeleti ve kritik motoru çalışmaktadır:

1. **Temel UI / UX Altyapısı:** Blazor Hybrid ile tüm platformlara (Mobil ve Masaüstü) uyumlu tek bir UI kod tabanı yazıldı. Dashboard ve ApexCharts entegrasyonu tamamlandı.
2. **Çevrimdışı (Offline) Destek:** İnternet yokken ders/görev (StudySession) oluşturma, durdurma ve bunu yerel SQLite veritabanına `OutboxQueue` olarak kaydetme.
3. **Senkronizasyon Motoru:** Ağ bağlantısı geldiğinde Outbox verilerini `SyncedStudySessionApiService` aracılığıyla API'ye basma (replay) ve hatalı işlemleri (Dead-Letter) loglama.
4. **JWT & Kimlik Doğrulama:** MAUI üzerinden API'ye otomatik token (`AuthorizationMessageHandler`) aktarılması.
5. **Windows Spesifik UI:** Pencere dışı API etkileşimleri ve masaüstü bildirim mekanizmaları.

---

## 🚧 3. Yayına Kadar Kalan Teknik Müdahaleler (Bugs & Tech Debt)

Bu kısımdaki maddeler projenin uygulama marketlerine ve son kullanıcıya gidebilmesi için mutlak çözülmesi gereken **"Mühendislik/Güvenlik"** borçlarıdır.

| Kategori | Görev / Eksiklik | Tehlike Derecesi |
| :--- | :--- | :--- |
| **Güvenlik** | API JWT Secret Key'in (`appsettings.json` içinden) Environment Variable'a taşınması. | 🔴 Yüksek |
| **Güvenlik** | Üretim ortamında CORS politikasının kısıtlanması ve dışarıdan rastgele API çağrılarının kesilmesi. | 🔴 Yüksek |
| **İş Mantığı** | "Her yeni gelen Premium'dur" sahte (`Demo`) yapısının kaldırılarak gerçek Premium algoritmasının Server'da kilitlenmesi. | 🟠 Orta |
| **UX / Ağ** | Uzun oturumlardaki sadece bir Access Token yerine Refresh Token senaryosunun yazılması (Token bittikçe kullanıcıyı dışarı atmaması için). | 🟠 Orta |
| **Performans** | Dashboard offline açıldığında "Pending Tasks" hesaplamasının doğru simüle edilmesi. | 🟡 Düşük |
| **Stabilite** | Outbox (Çevrimdışı kayıt) esnasında Serialization hatası alınırsa veriyi başarılı sayıp silmek yerine *Retry (Tekrar)* mekanizmasının çalıştırılması. | 🟠 Orta |
| **Temizlik** | Projedeki kullanılmayan test/build çöplerinin `build_errs.txt`, `TestApi.cs` gibi dosyaların repodan ayıklanması. | 🟡 Düşük |

---

## 🚀 4. "Yayına (Launch)" Giden İşletim Süreci 

Kodlamadan (Mühendislikten) ziyade, Ürünü piyasaya (Production) çıkarma adımları.

### A. Altyapı ve Sunucu Yayını
- **Veritabanı Yayını:** Geliştirme ortamı veritabanından buluttaki (Azure, AWS veya VPS) bir MSSQL/PostgreSQL sunucusuna geçiş.
- **Backend Publish:** API'nin Cloud üzerine `Release` modda deploy edilmesi ve SSL/HTTPS sertifikasının zorunlu (Enforced) yapılması.

### B. Mobil Uygulama Market Gereksinimleri
- Android için `AndroidManifest.xml`, iOS için `Info.plist` dosyalarının yasal gereksinimlere (izin yetkilerine) göre düzenlenmesi. (Örn: İnternet kullanımı, bildirim vb.)
- Uygulamaya özgün İkon (Icon) ve Sıçrama/Açılış Ekranı (Splash Screen) SVG/PNG dosyalarının MAUI içerisine tam yerleşimi.
- Her iki platform için Geliştirici Hesap onay süreçleri, sertifikalar (`.keystore` ve `.p12`) ile imzalama (Signing) işlemleri.

### C. Masaüstü Yayın
- Windows Package (MSIX) veya standart Bağımsız (Self-Contained) Exe çıktı türüne karar verilip `Windows Package Type` güncellemelerinin yapılması.
- Windows tarafındaki donanım kimliği (`System.Management`) erişiminin Anti-Virüs false-positive durumlarına karşı test edilmesi.

### D. Nihai Çıktılar
Uygulama tam bittiğinde elimizde 3 temel çıktı dosyası olacak:
1. `com.studytime.apk` / `.aab` (Google Play)
2. `StudyTime.ipa` (Apple AppStore)
3. `StudyTime.win.msix` / `.exe` (Microsoft Store veya Doğrudan Kurulum)

---
*Bu rapor, mevcut projenin kaynak kod dizinleri ve bağımlılık haritası üzerinden oluşturulan anlık durum özetidir.*
