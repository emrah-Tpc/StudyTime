# StudyTime — Günlük Aksiyon Planı

Toplam kalan 18 açığı kapatmak için günde 2 aksiyon alacak şekilde oluşturulmuş, bugünden (21 Nisan 2026) başlayan 9 günlük iş programı.

Daha teknik ve bağımlılığı yüksek görevleri ilerleyen günlere yayarken, hızlıca yapılabilecek "hızlı kazanımlar (quick wins)" veya güvenlik temellerini ilk günlere aldık.

## 📅 Gün 1: (21 Nisan 2026, Salı)
Öncelikle projenin düzene girmesi ve kullanıcı etkileşimini doğrudan etkileyen basit ama önemli iki kısmı çözüyoruz.
- [x] **1. Görev E2:** Görev listesi sıralamasını `Id` yerine `CreatedAt` gibi anlamlı bir alana göre ayarlamak. Kullanıcının "en son eklenen görevi en üstte" görmesini sağlarız.
- [x] **2. Görev F2:** Repodan geçici dosyaları (`build_errs.txt`, gereksiz class'lar, klasörler) temizleyip `.gitignore` dosyasını güncellemek.

## 📅 Gün 2: (22 Nisan 2026, Çarşamba)
API rotalarının temel güvenliği ve izinlerini ayarlıyoruz.
- [x] **1. Görev A3:** `Login` / `Register` işlemleri için `[AllowAnonymous]`, diğer tüm route'lar için ise `[Authorize]` kullanım politikasını uygulamak. Neresi açık, neresi kapalı garantilenir.
- [x] **2. Görev E1:** Geliştirme sürecindeki "açık" CORS politikasını, üretim ortamında (Prod) spesifik istemcilere sınırlayacak şekilde daraltmak. 

## 📅 Gün 3: (23 Nisan 2026, Perşembe)
Sunucu güvenliğini bir tık daha artırmaya odaklanıyoruz.
- [x] **1. Görev A1:** Hardcode yazılmış (veya sadece appsettings'te açık duran) JWT sırrını ortadan kaldırmak; gizlilik anahtarını ortam değişkeni veya secret maneger ile okuyacak yapı kurmak.
- [x] **2. Görev F3:** Geliştirme ortamındaki (Development) rahat TLS özelliklerini kaldırıp, üretime çıkış için zorunlu HTTPS/TLS kurallarını yapılandırmak.

## 📅 Gün 4: (24 Nisan 2026, Cuma)
İleri seviye JWT iyileştirmelerini yapıyoruz.
- [x] **1. Görev A4:** İstemci tarafı JWT claim haritalandırmasını standart JWT spesifikasyonlarına (ör. `sub` → `NameIdentifier`) göre düzenleyerek olası rol sorunlarını gidermek.
- [x] **2. Görev A2:** Sadece tek ve uzun süreli token kullanımı yerine, Refresh token veya daha güvenli bir Access + Refresh jwt mimarisini oturtmak. 

## 📅 Gün 5: (25 Nisan 2026, Cumartesi)
Premium iş mantığına giriş.
- [x] **1. Görev B3:** Kayıt modülünde deneme amaçlı "herkes premium başlar" kodu/bayrağını kapatıp, varsayılan ücretsiz modele geçişi sağlamak.
- [x] **2. Görev B1:** İstemciler arası uyumsuzlukları gidermek için `X-Hardware-Id` donanım kimliği zorunluluğunu (özellikle mobilde) net bir kurala bağlamak.
- [x] **3. Ek Görev (State Bleeding Kontrolü):** `GlobalTimerService`'de yaşanan "çıkış yapıldığında önceki kullanıcının verisinin hafızada kalması" sorununa benzer olarak, uygulamadaki diğer `Singleton` servislerin de kullanıcı çıkışında temizlenip temizlenmediğini incelemek ve gerekiyorsa `LocalDataWipeService` içerisine temizleme metotları eklemek.

## 📅 Gün 6: (26 Nisan 2026, Pazar)
Premium modülünün sağlamlaştırılması ve veri katmanı filtrelerinin düzenlenmesi.
- [x] **1. Görev B2:** İstemci tarih kontrollerini kaldırıp, Premium erişim mantığını bütünüyle Sunucu (Server-Side) odaklı doğrulanacak şekilde ayarlamak.
- [x] **2. Görev E4:** EF Core tarafında `HttpContext` olmayan arka plan servisleri vb. durumlarda Global Query filter (`UserId = `) davranışlarını test etmek ve uyumlandırmak.

## 📅 Gün 7: (27 Nisan 2026, Pazartesi)
İstemci tarafındaki çevrimdışı UX iyileştirmeleri.
- [x] **1. Görev E3:** Çevrimdışı/oturum geçişlerinde local cache görünürlüğünü koruyacak şekilde bildirim ve veri görünürlük fallback akışları güçlendirildi; notification center başlangıç yüklemesi eklendi.
- [x] **2. Görev C3:** Sayaç bitişindeki Stop/Resume/Pause yarışları ve stale-id senaryoları idempotent davranışla güvence altına alındı; outbox/reconcile akışıyla uyumlu hale getirildi.

## 📅 Gün 8: (28 Nisan 2026, Salı)
Çevrimdışı Outbox (Aktarılamayan Kuyruklar) hatalarının idaresi.
- [ ] **1. Görev D2:** Kuyruktan okunan (okunamayan null) verilerin veya Deserialize / Validate hatalarının, direkt başarılı sayılıp silinmesi yerine bir Retry (tekrar et) durumu almasını sağlamak.
- [ ] **2. Görev D3:** 3 kuyruk hatası sonrası veriyi "Dead Letter" tablosundan dahi kalıcı olarak "agresif silme" yerine, uyarı atacak bir operatör yaklaşımı kurgulamak.

## 📅 Gün 9: (29 Nisan 2026, Çarşamba)
Projenin ağ senkranizasyonu mükemmelleştirmeleri ve kalite ölçümü.
- [ ] **1. Görev D4:** Aynı çevrimdışı paketin sunucuya 2 kez gitmesini önlemek adına ID based "idempotency" (Çoklu işlemlerin tek işlemmiş gibi sayılması) özelliğini eklemek.
- [ ] **2. Görev F1:** Özellikle Auth, CRUD ve Sesion modülleri üzerine birkaç temel Entegrasyon Testi yazarak, bir sonraki eklenecek özellikle birlikte kodun zedelenmediğini garanti altına almak.

---

## ✅ Yapılanlar (Log)

**Gün 1:**
- **Sorun:** Görev listesi kullanıcıya anlamsız sıralama ile gösteriliyordu (`Id` bazlı).  
  **Çözüm:** Sıralama `CreatedAt` alanına taşındı, yeni eklenen görevlerin üstte görünmesi sağlandı (Görev E2).
- **Sorun:** Repoda geçici/çöp dosyalar build ve çalışma düzenini kirletiyordu.  
  **Çözüm:** Geçici dosyalar temizlendi, `.gitignore` güncellendi (Görev F2).

**Gün 2:**
- **Sorun:** API endpointleri için açık/kapalı erişim sınırı net değildi.  
  **Çözüm:** Global `AuthorizeFilter` ile secure-by-default kuruldu; sadece `Login/Register` anonim bırakıldı (Görev A3).
- **Sorun:** CORS politikası geliştirme odaklı genişti, üretimde risk oluşturuyordu.  
  **Çözüm:** Dev/Prod CORS ayrıştırıldı; prod’da sadece `AllowedOrigins` whitelist çalışacak şekilde sıkılaştırıldı (Görev E1).

**Gün 3:**
- **Sorun:** JWT secret kaynak kod/config içinde riskli konumdaydı.  
  **Çözüm:** Secret ortam değişkenine taşındı; production’da zorunlu, development’da kontrollü fallback ile fail-fast kuralı eklendi (Görev A1).
- **Sorun:** Üretim güvenlik katmanı HTTPS/TLS açısından eksikti.  
  **Çözüm:** `RequireHttpsMetadata` + `UseHsts` ile prod güvenliği sertleştirildi (Görev F3).
- **Sorun:** Secret taşınması sonrası oluşan 401 etkisi izlenebilir değildi.  
  **Çözüm:** Hata çözümü dokümana işlendi: [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

**Gün 4:**
- **Sorun:** JWT claim map uyumsuzluğu kimlik/rol çözümlemesini zayıflatıyordu.  
  **Çözüm:** `sub` -> `ClaimTypes.NameIdentifier` eşlemesi standart hale getirildi (Görev A4).
- **Sorun:** Tek token mimarisi kısa ömür/oturum sürdürülebilirliği açısından kırılgandı.  
  **Çözüm:** Access + Refresh token mimarisi, `/api/auth/refresh` endpointi ve istemcide sessiz yenileme akışı eklendi (Görev A2).

**Gün 5:**
- **Sorun:** Platformlar arası oturum/refresh token çakışmaları yaşanıyordu.  
  **Çözüm:** `AppUser` platform bazlı token/HWID alanlarıyla ayrıştırıldı; desktop/mobile oturumları izole edildi (Görev B1).
- **Sorun:** Desktop erişim kuralı premium tarafında net uygulanmıyordu.  
  **Çözüm:** Login aşamasında premium/pro zorunluluğu eklendi; uygun olmayan kullanıcı `403` alır hale getirildi.
- **Sorun:** Cihaz doğrulama filtresi anonim endpointleri de etkileyebiliyordu.  
  **Çözüm:** `ActiveSessionFilter` yeniden düzenlendi; anonymous geçişi korundu, authenticated isteklerde `X-Hardware-Id` zorunlu yapıldı (Görev B1).
- **Sorun:** Donanım kimliği üretimi platforma göre standardize değildi.  
  **Çözüm:** `IDeviceIdentityService` + Windows/Mobile implementasyonları ve platform bazlı DI kayıtları eklendi (Görev B1).
- **Sorun:** Session zaman hesapları istemci saat manipülasyonuna açıktı.  
  **Çözüm:** Zaman hesapları `UtcNow` tabanına taşındı.
- **Sorun:** Aynı kullanıcı için paralel aktif session açılabiliyordu.  
  **Çözüm:** DB filtered unique index + servis düzeyinde aktif session kontrolü + `409 ACTIVE_SESSION_EXISTS` akışı eklendi.
- **Sorun:** İkinci cihazdaki aktif session görünürlüğü eksikti.  
  **Çözüm:** `GET /api/StudySession/active` endpointi eklendi.
- **Sorun:** Logout tüm platform oturumlarını etkileyebiliyordu.  
  **Çözüm:** `POST /api/auth/logout` platform HWID eşleşmesine göre sadece ilgili platform token/HWID bilgisini temizleyecek şekilde eklendi.
- **Sorun:** Logout sonrası singleton state bleeding riski vardı.  
  **Çözüm:** `LocalDataWipeService` içinde sync/notification state resetleri eklendi (Ek Görev).
- **Sorun:** İstemci auth akışı platform/HWID doğrulamasıyla tam uyumlu değildi.  
  **Çözüm:** Client `AuthService` yeniden düzenlendi; register/logout ve header akışları netleştirildi.
- **Sorun:** Migration geçmişi dağınık ve gürültülüydü.  
  **Çözüm:** Yeni migration uygulandı, boş migration kayıtları temizlenip geçmiş sadeleştirildi.
- **Sorun:** Yapılan değişikliklerin entegrasyon güveni düşüktü.  
  **Çözüm:** 10 senaryoluk entegrasyon testi çalıştırıldı, **10/10 PASS** alındı.

**Gün 6:**
- **Sorun:** Premium kontrolü istemci saatine/yerel state’e fazla bağımlıydı.  
  **Çözüm:** Local `exp/PremiumUntil` logout akışı kaldırıldı; kararlar API yanıtı merkezine alındı (Görev B2).
- **Sorun:** Premium erişim kuralları controller’larda dağınık/tekrarlıydı.  
  **Çözüm:** `ISubscriptionAccessService` ile servis katmanında merkezileştirildi (Görev B2).
- **Sorun:** `AppUser` premium helper kuralları edge-case’lerde yetersizdi.  
  **Çözüm:** `SubscriptionType` (Lifetime dahil) kurallarıyla güçlendirildi (Görev B2).
- **Sorun:** Global query filter null-user/system-context durumlarında net güvenlik davranışı vermiyordu.  
  **Çözüm:** `IsSystemContext` modeli eklendi; tenant bypass sadece explicit system context ile sınırlandı, soft-delete her durumda korundu (Görev E4).
- **Sorun:** Bu güvenlik davranışları testle garanti altında değildi.  
  **Çözüm:** `StudyTime.Infrastructure.Tests` ile tenant izolasyonu, null-user ve soft-delete senaryoları doğrulandı (Görev E4).
- **Sorun:** Login hata kodları istemci tarafında ayrıştırılamıyordu.  
  **Çözüm:** `AuthController.Login` response kodları standardize edildi (`401/403` ayrımı).
- **Sorun:** Dashboard view migration zinciri eksik/bozuktu.  
  **Çözüm:** View migration + snapshot yeniden üretildi, soft-delete filtreleri SQL view seviyesinde zorunlu kılındı.
- **Sorun:** View mapping ve migration uygulanma doğrulaması eksikti.  
  **Çözüm:** `ToView("v_DashboardSummary", "dbo") + HasNoKey()` ile mapping netleştirildi, DB/migration history doğrulandı.
- **Sorun:** Desktop istemcide `HttpClientFactory` recursion kaynaklı runtime hata vardı.  
  **Çözüm:** Refresh akışı `AuthService` bağımlılığından çıkarıldı, `StudyTimeApiNoAuth` client’a taşındı.
- **Sorun:** Auth çağrılarında client sorumluluk ayrımı net değildi.  
  **Çözüm:** `login/register/refresh` -> `StudyTimeApiNoAuth`, authenticated çağrılar -> `StudyTimeApi` olacak şekilde ayrıştırıldı.

**Gün 7:**
* **Sorun:** Sayaç bitişinde (`StopSession`) 401/oturum düşüşü sonrasında veriler uygulamada "gitmiş" gibi görünüyordu.  
  **Çözüm:** `CustomAuthenticationStateProvider` içinde otomatik logout akışında local owner context korunacak şekilde güncellendi; `wipeLocalData=false` senaryosunda owner/profile anahtarları silinmiyor, token yokken owner-sub fallback ile cache görünürlüğü korunuyor.
* **Sorun:** 1-5 dk token testlerinde refresh mekanizması 401 `Invalid token` dönüyor, sessiz yenileme çalışmıyordu.  
  **Çözüm:** API `AuthService.GetPrincipalFromExpiredToken` algoritma doğrulaması `HS256`/`HmacSha256` varyantlarını kabul edecek şekilde düzeltildi; canlı testte expiry sonrası `401 -> refresh 200 -> retry 200` akışı doğrulandı.
* **Sorun:** Paralel 401 isteklerinde refresh yarış koşulu oluşup ikinci istekler eski refresh token ile düşüyordu.  
  **Çözüm:** `AuthorizationMessageHandler` içine single-flight refresh kilidi (`SemaphoreSlim`) eklendi; kilit öncesi/sonrası token karşılaştırmasıyla gereksiz ikinci refresh engellendi.
* **Sorun:** Tarih aralığı görev sorgusu kısmi sonucu tüm task cache üzerine `ReplaceAll` yaparak veri kaybı algısı yaratıyordu.  
  **Çözüm:** `SyncedTaskApiService.GetTasksByDateRangeAsync` içinde `ReplaceAll` kaldırılıp `UpsertAll` kullanıldı; kısmi endpoint artık tüm cache'i silmiyor.
* **Sorun:** Timer notification zinciri her zaman aktif olmayabiliyor, uygulama açılışında notification listesi boş kalabiliyordu.  
  **Çözüm:** `App.xaml.cs` içinde `TimerNotificationService` startup'ta zorunlu resolve edildi; `AppNotificationCenterService` ctor'da ilk `LoadNotificationsAsync()` çağrısı eklendi.
* **Sorun:** Notification cache'te kullanıcı bağlamı boş olduğunda bildirimler hiç görünmüyordu.  
  **Çözüm:** `LocalNotificationCache` içinde `LocalOwnerSub` fallback'i eklendi; user context eksikse bile kayıtları okuyup işaretleyebilen güvenli fallback akışları tanımlandı.

**Kritik Not (30 Nisan 2026):**
* **Sorun:** Desktop client'ta bildirim akışı eksikti: kronometreyi manuel durdurunca bildirim üretilmiyor, yaklaşan görevler için (deadline yakın) otomatik uyarı hiç tetiklenmiyordu; kullanıcı bildirim kutusunda beklenen uyarıları göremiyordu.  
  **Çözüm:** Bildirim pipeline'ı kalıcı olarak genişletildi: `GlobalTimerService` içine `OnTimerStopped` eventi eklendi, `TimerNotificationService` bu event için sistem bildirimi üretir hale getirildi ve yeni `TaskReminderNotificationService` ile bitişe 30 dk kalan `Pending` görevler dakikalık taranıp notification center'a otomatik düşürülüyor (duplicate önleme ile).

