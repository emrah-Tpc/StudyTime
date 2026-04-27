# StudyTime — İş Akışı, Checklist ve İyileştirme Raporu

Bu belge, çözüm mimarisine dayanan iş akışı özeti, sıralı checklist ve önceliklendirme önerisini içerir. Kod değişikliği değildir; uygulama ve operasyon için yol haritasıdır.

Tamamlanan maddeler için `- [ ]` yerine `- [x]` kullanın (GitHub / birçok Markdown görüntüleyicide tıklanabilir).

---

## 1. Ürün ve teknik iş akışı (sıralı özet)

### Son kullanıcı akışı (yüksek seviye)

1. Kayıt / giriş  
2. JWT ile API erişimi  
3. Ders (lesson) ve görev (task) yönetimi  
4. Çalışma oturumu (study session) başlat / durdur  
5. Dashboard / istatistik  
6. (Opsiyonel) çevrimdışı kullanım ve yeniden bağlanınca senkron  

### Teknik akış (arka planda)

- **İstemci:** MAUI Blazor → `HttpClient` + `AuthorizationMessageHandler` (Bearer + Windows’ta `X-Hardware-Id`).  
- **Sunucu:** JWT doğrulama → `ActiveSessionFilter` (HWID / premium) → Controller → Application servisleri → EF Core (`StudyTimeDbContext`: global filtre + `UserId` ataması).  
- **Çevrimdışı:** SQLite önbellek + `OutboxQueue` → `OutboxProcessor` ile API’ye replay.  

---

## 2. Checklist (sırayla)

Her satırda: **yapılacak iş** → **neden** → **avantaj** → **şu anki eksiklikten doğan dezavantaj**.

### A. Kimlik doğrulama ve oturum

| Durum | # | Yapılacak iş | Neden | Avantaj | Şu anki dezavantaj (gap) |
|--------|---|--------------|-------|---------|---------------------------|
| - [ ] | A1 | JWT gizli anahtarını yalnızca `appsettings` yerine ortam değişkeni / gizli kasa ile yönetmek | Anahtar sızarsa herkes token üretebilir | Üretim güvenliği, rotasyon kolaylığı | Sabit secret repoda kalırsa sızıntıda tüm oturumlar riske girer |
| - [ ] | A2 | Token yenileme (refresh) veya kısa ömürlü access + yenileme akışı tasarlamak | Uzun ömürlü tek token riski azalır | Çalıntı token etkisi sınırlanır; `PremiumUntil` sunucu ile uyum kolaylaşır | Sadece uzun JWT + claim’teki `PremiumUntil` ile istemci “süresi doldu” kararı verir; sunucuda uzatılmış üyelik token eskiyken yanlış çıkış |
| - [ ] | A3 | Login/Register için net `[AllowAnonymous]` ve diğer endpoint’lerde zorunlu `[Authorize]` politikasını dokümante etmek | Yanlışlıkla korumasız endpoint kalmaz | Denetlenebilir güvenlik modeli | Politika tek yerde toplanmalı |
| - [ ] | A4 | İstemci JWT claim ayrıştırmasını standart `sub` → `NameIdentifier` eşlemesi ile uyumlu hale getirmek (gerekirse) | Sunucu ve istemci aynı kimlik kavramını kullanır | Rol/claim tabanlı UI koşulları güvenilir | Ham dictionary claim’ler bazı senaryolarda tutarsız davranışa yol açabilir |

### B. Cihaz bağlama (HWID) ve premium

| Durum | # | Yapılacak iş | Neden | Avantaj | Şu anki dezavantaj |
|--------|---|--------------|-------|---------|---------------------|
| - [ ] | B1 | `X-Hardware-Id` olmadan isteklerde politika netleştirmek: mobilde de başlık veya alternatif cihaz kimliği ya da “HWID zorunlu değil” bilinçli kararı | Şu an başlık yoksa filtre tamamen atlanıyor | Tek cihaz / premium kuralları her platformda öngörülebilir | Windows dışında başlık yok → `ActiveSessionFilter` premium ve HWID kontrollerini atlıyor; kurallar platforma göre farklı |
| - [ ] | B2 | Premium süresi için sunucu tek doğruluk kaynağı; istemci sadece UX | Ödeme / abonelik değişince anında doğru davranış | Haksız erişim veya haksız red azalır | İstemci sadece token’daki tarihe bakıyorsa sunucu ile sapma |
| - [ ] | B3 | Kayıtta “herkes premium” demo bayrağını kaldırıp gerçek abonelik modeline bağlamak | Ürün mantığı ile uyum | Gelir modeli ve test senaryoları gerçekçi | Şu an kayıtta otomatik premium; üretimde yanlış varsayılan |

### C. Çevrimdışı çalışma oturumu (Outbox — kritik)

| Durum | # | Yapılacak iş | Neden | Avantaj | Şu anki dezavantaj |
|--------|---|--------------|-------|---------|---------------------|
| - [x] | C1 | Offline Start replay’inde sunucunun döndürdüğü `SessionId`’yi kaydedip Stop ile eşleştirmek | Yerel GUID ≠ sunucu GUID | Sunucuda oturum doğru açılıp kapanır | `SessionServerIdMap` + `SyncedStudySessionApiService` çözümlemesi (2026) |
| - [x] | C2 | Outbox’ta `StudySession/Stop` için gerçek API çağrısı (veya Start+Stop tek bir “kayıtlı süre” endpoint’i) | Sunucu verisi ile istemci özeti uyumlu | İstatistik ve dashboard doğru | Stop replay sunucu Id ile `StopSessionAsync` (2026) |
| - [ ] | C3 | Pause/Resume offline stratejisini netleştirmek: ya kuyruğa yazmak ya da bitişte toplam süreyi tek sefer göndermek | Sunucu süre hesabı doğru | Raporlarda sapma olmaz | Offline’ta pause/resume yok sayılıyor; uzun oturumlarda fark büyür |
| - [x] | C4 | `SyncedStudySessionApiService` DI kaydında açılan `IServiceScope`’u `using` ile dispose etmek | Scoped servis ömrü doğru | Bellek sızıntısı ve garip HttpClient kullanımı önlenir | `IServiceScopeFactory` + `AddSingleton`; her çağrıda `using` scope (2026) |

### D. Outbox genel güvenilirlik

| Durum | # | Yapılacak iş | Neden | Avantaj | Şu anki dezavantaj |
|--------|---|--------------|-------|---------|---------------------|
| - [x] | D1 | Bilinmeyen `EntityType` / `Operation` için `return true` + silme yerine log + dead-letter tablosu veya manuel müdahale kuyruğu | Sessiz veri kaybını engeller | Hata ayıklanır, veri kurtarılabilir | `DeadLetterQueue` SQLite + `ILogger` uyarısı; 3 deneme sonrası taşıma (2026) |
| - [ ] | D2 | Deserialize / validasyon başarısızlığında başarılı sayma yerine retry veya hata durumu | Bozuk payload’lar fark edilir | Kısmi senkron yanıltıcı olmaz | Null DTO ile bile `true` dönülebiliyor; iş yapılmadan kayıt düşer |
| - [ ] | D3 | 3 deneme sonrası kalıcı silme yerine üst sınır + alarm veya operatör ekranı | Geçici API kesintilerinde veri korunur | Kullanıcı güveni | Agresif silme kalıcı veri kaybı |
| - [ ] | D4 | Replay sırasında idempotency (aynı offline işlemin iki kez uygulanmaması) düşünmek | Ağ tekrarlarında çift kayıt olmaz | Veri tutarlılığı | Tekrarlı flush senaryolarında risk |

### E. API ve veri katmanı

| Durum | # | Yapılacak iş | Neden | Avantaj | Şu anki dezavantaj |
|--------|---|--------------|-------|---------|---------------------|
| - [ ] | E1 | CORS politikasını üretimde belirli origin’lere indirgemek | Tarayıcı üzerinden kötü niyetli sitelerin API çağrısı riski azalır | Saldırı yüzeyi küçülür | `AllowAnyOrigin` üretimde geniş yüzey |
| - [ ] | E2 | Görev listesi sıralamasını `Id` (Guid) yerine `CreatedAt` / `UpdatedAt` gibi anlamlı alanla yapmak | Kullanıcı “en son” beklentisi | UX ve destek talepleri azalır | `OrderByDescending(Id)` kronolojik sıra vermez |
| - [ ] | E3 | Dashboard offline deltası (`PendingTasks`, tamamlananlar) için daha doğru kurallar veya “tahmini” etiketi | Sayılar güvenilir | Kullanıcı güveni | Sadece create/delete farkı pending’e ekleniyor; tamamlama vb. yok |
| - [ ] | E4 | Arka plan job / migration gibi HttpContext’siz `DbContext` kullanımında global filtre davranışını dokümante etmek veya ayrı context | Boş sonuç / yanlış yazım önlenir | Operasyonel güven | `UserId` null iken filtre davranışı kafa karıştırıcı olabilir |

### F. Geliştirme süreci ve kalite

| Durum | # | Yapılacak iş | Neden | Avantaj | Şu anki dezavantaj |
|--------|---|--------------|-------|---------|---------------------|
| - [ ] | F1 | Entegrasyon testleri: auth, task CRUD, session start/stop, outbox replay | Regresyonları erken yakalar | Refactor güveni | Test projesi yoksa her değişiklik riskli |
| - [ ] | F2 | Repodan geçici dosyaları (`build_errs.txt`, `TestApi.cs` vb.) ayıklamak veya `.gitignore` | Temiz geçmiş ve CI | Onboarding hızlanır | Gürültü ve yanlış “kaynak” algısı |
| - [ ] | F3 | HTTPS / sertifika: geliştirmede DEBUG bypass bilinçli; üretimde zorunlu TLS | Ortadaki adam riski | Veri gizliliği | `RequireHttpsMetadata = false` geliştirme ayarı üretime taşınırsa risk |

---

## 3. Önceliklendirme önerisi

- [x] **1.** C1, C2, C4 (oturum senkronu + scope) — tamamlandı; **C3** açık.  
- [x] **2.** D1 (dead-letter + log) — tamamlandı; **D2–D3** açık.  
- [ ] **3.** B1–B3 (HWID/premium tutarlılığı) — iş kuralları ve güvenlik.  
- [ ] **4.** A1–A2, E1 (secret, refresh, CORS) — üretim güvenliği.  
- [ ] **5.** E2–E3, F1 — UX ve sürdürülebilirlik.  

---

## 4. Özet

İş akışı uçtan uca mantıklı ve modern bir çekirdek (Identity + JWT + EF global tenant + offline kuyruk) üzerinde. En büyük boşluklar:

- Çalışma oturumunun offline’tan sunucuya **güvenilir taşınması**  
- Outbox’un **“başarılı sayıp silme”** eğilimi  
- **Platforma göre değişen** HWID/premium denetimi  
- **Üretim güvenlik** ayarları (secret, CORS, TLS politikası)  

Checklist’i sırayla işaretlemek, önce veri doğruluğunu sonra güvenliği kilitlemenizi sağlar.

---

*Son güncelleme: bu belge proje incelemesine göre oluşturulmuştur; maddeler tamamlandıkça `Durum` sütununda `- [ ]` → `- [x]` yapın veya aşağıdaki öncelik maddelerini işaretleyin.*
