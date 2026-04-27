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
- [ ] **1. Görev E3:** Çevrimdışı modda Dashboard ekranında görünen eksik bildirimleri veya beklemedeki (PendingTasks) detayını "Tahmini Değer" verileri ile iyileştirmek.
- [ ] **2. Görev C3:** Çevrimdışı moddayken gerçekleşecek "Durdur / Devam Et" (Pause/Resume) davranışının nasıl bir stratejiyle sunucuya yansıyacağını kararlaştırıp kodlamak.

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
* Görev listesi sıralaması `CreatedAt` üzerinden ('Id' yerine) düzeltildi (Görev E2).
* Gereksiz/Geçici log ve class dosyaları projeden temizlendi, `.gitignore` güncellendi (Görev F2).

**Gün 2:**
* `Program.cs` içerisine `AuthorizeFilter` eklenerek API genelinde zorunlu kimlik denetimi aktifleştirildi (Secure by default). Sadece `AuthController` içerisindeki `Login` ve `Register` metotları dışarıya (`[AllowAnonymous]`) açıldı (Görev A3).
* Geliştirme (Development) ve Üretim (Production) ortamları için CORS kuralları birbirinden ayrıldı. Üretim ortamında `CorsSettings:AllowedOrigins` değerinden okunan adreslerin izinli olduğu sıkı politika yapılandırıldı (Görev E1).

**Gün 3:**
* `appsettings.json` içerisindeki JWT Sırrı (Secret) ortam değişkenlerine bağlandı (Görev A1). Geliştirme (Development) sürecinde sorunsuz çalışması için hardcode "fallback" eklendi; Üretim (Production) sürecinde ise değer girilmediyse anında `InvalidOperationException` hatası vermesi sağlandı (Fail-Fast kurgusu).
* API projesindeki Security katmanı güçlendirilerek canlı taraf için (Production) zorunlu HTTPS/TLS kuralları oturtuldu (`RequireHttpsMetadata = true`). Katmanlı güvenlik olarak HTTP Strict Transport Security (`app.UseHsts();`) eklendi (Görev F3).
* **Not:** JWT Secret değerinin ortam değişkenine taşınmasından kaynaklı ortaya çıkan 401 Unauthorized (şifre hatalı) hatasının çözümü [TROUBLESHOOTING.md](TROUBLESHOOTING.md) belgesine not edilmiştir.

**Gün 4:**
* İstemci tarafında `ParseClaimsFromJwt` metodu güncellendi, `sub` claim'i `.NET` standartlarına uyması adına `ClaimTypes.NameIdentifier` olarak haritalandırıldı (Görev A4).
* Access Token'ın yanına Refresh Token eklendi. `AppUser` modeline Refresh Token alanları eklendi. `AuthService`'e `/api/auth/refresh` metodu eklendi. İstemci tarafında `AuthorizationMessageHandler` ile 401 Unauthorized hataları yakalanıp, arka planda otomatik (sessiz) token yenileme mekanizması devreye alındı (Görev A2).

**Gün 5:**
* `AppUser` entitesi platform bazlı oturum yönetimine göre yeniden yapılandırıldı; tekil `CurrentActiveHwid` ve `RefreshToken` alanları kaldırılıp, `DesktopHwid`, `DesktopRefreshToken`, `DesktopRefreshTokenExpiryTime`, `MobileHwid`, `MobileRefreshToken`, `MobileRefreshTokenExpiryTime` alanları eklendi. Böylece mobil ve masaüstü oturumları birbirini etkilemeden eşzamanlı sürdürülebilir hale getirildi (Görev B1).
* Desktop login sadece aktif Premium/Pro kullanıcılara izin verecek şekilde `AuthService.LoginAsync` içerisine erişim kısıtı eklendi. Aboneliği olmayan kullanıcılar `403 PREMIUM_REQUIRED` hatasıyla reddedilir; premium kontrolü `ActiveSessionFilter`'a taşınmadı, login anında yapılır.
* `ActiveSessionFilter` yeniden yazıldı: `AllowAnonymous` endpointleri ve kimliği doğrulanmamış istekler filtreden geçiyor; kimliği doğrulanmış her istek için `X-Hardware-Id` başlığı zorunlu hale getirildi ve gelen HWID, kullanıcının kayıtlı `DesktopHwid` veya `MobileHwid` alanlarından biriyle eşleştirildi (Görev B1).
* `IDeviceIdentityService` arayüzü oluşturuldu. Windows için WMI tabanlı CPU + BaseBoard SHA256 hash'i üreten `WindowsDeviceIdentityService`; iOS/Android/Mac için `SecureStorage` (Preferences) tabanlı kalıcı GUID üreten `DeviceIdentityService` yazıldı. `MauiProgram.cs`'de `#if WINDOWS` koşuluyla platform uyumlu DI kaydı yapıldı (Görev B1).
* `StudySession` entitesindeki tüm zaman hesaplamaları `DateTime.Now` → `DateTime.UtcNow` olarak güncellendi; timer süresi artık sunucu saatiyle belirleniyor, istemci saatiyle manipülasyonun önüne geçildi.
* `StudySession` tablosuna `UserId` bazında filtered unique index eklendi (`EndedAt IS NULL AND IsDeleted = 0`). Aynı kullanıcı için aynı anda birden fazla aktif oturum açılması hem veritabanı hem de servis katmanında engellendi; ikinci cihazdan gelen `start` isteği `409 ACTIVE_SESSION_EXISTS` döner (Görev B3 kapsamı).
* `StudySessionRepository`'e `GetActiveSessionAsync(userId)` metodu eklendi. `StudySessionService.StartAsync` içinde aktif oturum kontrolü `UserId` bazında yapılıyor; `StudySessionController.Start` metodu `409 Conflict` döndürüyor.
* `GET /api/StudySession/active` endpointi eklendi. İkinci cihaz bu endpoint üzerinden açık olan oturumu sorgulayıp "devralma" (takeover) yapabilir; mevcut oturum üzerinden pause/stop emirleri gönderebilir.
* `POST /api/auth/logout` endpointi eklendi. Platform bilgisine göre (`X-Hardware-Id` eşleşmesi) yalnızca ilgili platformun HWID ve Refresh Token alanları temizlenerek diğer platform oturumu etkilenmez.
* `LocalDataWipeService`'e `SyncStatusService.Reset()` ve `AppNotificationCenterService.ClearAll()` çağrıları eklendi; kullanıcı çıkışında Singleton servislerdeki state bleeding tamamen önlendi (Ek Görev).
* İstemci `AuthService` yeniden yazıldı: `IDeviceIdentityService` üzerinden HWID alınıyor, `ClientType` `DeviceInfo.Idiom` ile belirleniyor, `RegisterAsync` metodu eklendi, `LogoutAsync` backend endpoint'ini tetikleyip ardından yerel temizlik yapıyor. `AuthorizationMessageHandler` platform bağımsız HWID başlığını her istekle gönderiyor.
* EF Core migration `PlatformBasedAuthAndSessionConstraint` oluşturuldu ve uygulandı. 4 adet içi boş (`SELECT 1;`) eski migration dosyası hem dosya sisteminden hem `__EFMigrationsHistory` tablosundan temizlendi; migration geçmişi 14 → 10 dosyaya indirildi.
* Tüm değişiklikler 10 maddelik entegrasyon testi ile doğrulandı (Mobile register/login, Desktop Free→403, Timer start, 409 çakışma, /active endpoint, pause, logout, logout sonrası 401) — **10/10 PASS**.

**Gün 6:**
* `CustomAuthenticationStateProvider` içindeki istemci saatine bağlı `exp` ve `PremiumUntil` kontrolünden kaynaklanan yerel logout akışı kaldırıldı; premium/onay kararı artık yalnızca API cevabı üzerinden yürütülüyor (Görev B2).
* Premium doğrulaması servis katmanına merkezileştirildi: `ISubscriptionAccessService`/`SubscriptionAccessService` eklendi, `AuthService.LoginAsync`, `LessonController` ve `DashboardController` bu servis üzerinden karar verir hale getirildi; controller içindeki manuel premium hesaplamaları kaldırıldı (Görev B2).
* `AppUser.HasActivePremium(DateTime utcNow)` helper'ı `SubscriptionType` kurallarıyla güçlendirildi (`Lifetime` destekli, `Free` hariç aktif abonelik kontrolü) ve tüm premium erişim kararları bu merkezi helper üzerinden yürütülmeye başlandı (Görev B2).
* `ICurrentUserService` modeline `IsSystemContext` eklendi. `StudyTimeDbContext` global query filter'ları artık `UserId` yoksa tenant verisini açmıyor; yalnızca açıkça system context işaretlendiğinde tenant bypass uygulanıyor. Soft-delete filtresi her durumda korunuyor (Görev E4).
* E4 güvenlik senaryoları için `StudyTime.Infrastructure.Tests` test projesi eklendi; tenant izolasyonu, null-user güvenli davranışı, system context erişimi ve soft-delete kuralları testlerle doğrulandı (Görev E4).
* `AuthController.Login` içindeki `UnauthorizedAccessException` yakalama akışı kod bazlı ayrıştırıldı: `INVALID_USER_CONTEXT` için `401`, `DESKTOP_PREMIUM_REQUIRED` için `403`, diğer yetkisiz durumlar için `401 UNAUTHORIZED` yanıtı dönülecek şekilde standartlaştırıldı.
* `DashboardSummaryView` için eksik kalan view migration akışı düzeltildi: `20260427170952_EnforceDashboardViewSoftDeleteFilters` migration'ı Designer + Snapshot ile yeniden üretildi, `Up()` içinde `CREATE OR ALTER VIEW [dbo].[v_DashboardSummary]` ile soft-delete filtreleri (`Tasks/StudySessions/Lessons IsDeleted = 0`) zorunlu hale getirildi, `Down()` tarafı `DROP VIEW IF EXISTS` ile güvenli geri dönüşe çekildi.
* View mapping `StudyTimeDbContext` içinde schema ile netleştirildi (`ToView("v_DashboardSummary", "dbo")` + `HasNoKey()`); migration DB'ye uygulandı ve SQL doğrulamasıyla `v_DashboardSummary` nesnesinin oluştuğu, `__EFMigrationsHistory` tablosuna migration kaydının işlendiği teyit edildi.
* Desktop istemcide `ValueFactory attempted to access the Value property of this instance` hatasına neden olan `HttpClientFactory` döngüsü giderildi: `AuthorizationMessageHandler` içindeki refresh akışı `AuthService` bağımlılığından çıkarıldı ve handler'sız `StudyTimeApiNoAuth` client'ına taşındı.
* `AuthService` içinde HTTP istemcileri sorumluluğa göre ayrıştırıldı: `login/register/refresh` çağrıları `StudyTimeApiNoAuth`, token gerektiren `logout/profile/password` çağrıları `StudyTimeApi` üzerinden çalışacak şekilde güncellendi; böylece auth bootstrap ve refresh sırasında handler recursion riski kapatıldı.
