# StudyTime — Çözüm Planı (Kod İncelemesi Sonrası)

> Bu belge, full-stack kod incelemesinde tespit edilen **tüm** bulguları kapsayan fazlı
> remediation planıdır. Her bulgu bir görev ID'sine bağlanmış, sonda izlenebilirlik
> matrisi ile "atlanan yok" garantisi verilmiştir.
>
> Durum: Planlandı · Başlangıç fazı: **Faz 0 (Keşif & Doğrulama)**

## Plan İlkeleri

1. **Önce kanıtla, sonra düzelt:** "Testle doğrulanmalı" maddeler (özellikle soft-delete
   istatistik kaybı) Faz 0'da reprodüksiyon testiyle kanıtlanır; varsa düzeltilir.
2. **Her düzeltme bir test bırakır:** Backend için xUnit (`StudyTime.Infrastructure.Tests`),
   kritik akışlar için entegrasyon testi.
3. **Tek kaynak ilkesi:** Süre/zaman/today gibi değerler tek yerden hesaplanır
   (View ↔ C# ikiliği biter).
4. **Geri-uyumlu ilerleme:** Her faz kendi içinde derlenir + testleri geçer; küçük PR'lar.
5. **Davranış değiştirmeden önce gözlemle:** Sessiz `catch`'ler kaldırılırken loglama
   eklenir, UX bozulmaz.

Efor: **S** (≤2s), **M** (yarım gün), **L** (1+ gün). Risk: regresyon olasılığı.

---

## Faz 0 — Keşif & Doğrulama

| ID | Görev | Dosyalar | Çıktı |
|---|---|---|---|
| D1 | Soft-delete edilen ders → o derse ait eski session'lar istatistikten düşüyor mu? Reprodüksiyon testi | `StudyTime.Infrastructure.Tests` + `StudySessionRepository.GetByDateRangeAsync` | Test kırmızıysa F03 devreye girer |
| D2 | UI state/leak: `GlobalTimerService` event abonelikleri `Dispose`'da kaldırılıyor mu? `MarkupString` (XSS)? loading/error/empty state'ler | `DashboardBase.cs`, `Workspace.razor`, `Statistics.razor`, `FocusMode.razor`, `NotificationCenter.razor`, `MainLayout.razor` | Leak/XSS bulgu listesi → Faz 3 |
| D3 | Offline sync tutarlılığı ve "0dk" kök neden | `LocalDb.cs`, `LocalTaskCache.cs`, `LocalLessonCache.cs`, `LocalSnapshotCache.cs`, `LocalSessionAnalytics.cs`, `LocalDataWipeService.cs` | Sync bug listesi |
| D4 | Şema ↔ entity uyumu (`Notifications.IsDeleted`, `v_DashboardSummary`) | `StudyTimeDbContextModelSnapshot.cs`, son 2 notification migration | Migration tutarlılık raporu |
| D5 | DTO/validation netliği | `CreateTaskDto.cs`, `UpdateTaskDto.cs`, `StartStudySessionDto.cs` | Eksik validator listesi (Faz 1) |

### Faz 0 — Sonuçlar (Tamamlandı)

| ID | Sonuç |
|---|---|
| **D1** | ✅ **F03 KANITLANDI.** `StudyTime.Infrastructure.Tests/SoftDeleteStatisticsTests.cs` (SQLite ilişkisel provider) ile gösterildi: ders soft-delete edilince `GetByDateRangeAsync` (`.Include(Lesson)`) o derse ait oturumları **düşürüyor** (Include'suz 1 satır, Include'la 0 satır). İstatistik/dashboard'da geçmiş süre geriye dönük kaybolur. Test şu an mevcut hatalı davranışı belgeliyor (suite yeşil); **F03 düzeltilince `Assert.Empty` → `Assert.Single` çevrilecek** ve regresyon testine dönüşecek. Not: SQLite FK zorlaması mevcut InMemory testlerinde olmayan bir gerçekçilik kattı (AppUser FK). |
| **D2** | ✅ `Workspace.razor` ve `DashboardBase.cs` event aboneliklerini `Dispose`'da doğru kaldırıyor → **memory leak yok**. Tüm client'ta `MarkupString/Html.Raw` **yok** → **XSS yok**. Yeni küçük bulgular: (a) `async void` event handler'lar — `Workspace.OnTimerFinished`, debounce `Elapsed`, `SaveNotesAuto` try/catch'siz → unobserved exception (→ F14/F44). (b) `DashboardBase.SoftReload/LoadAsync` boş `catch {}` → kullanıcıya **error state gösterilmiyor** (→ F45). |
| **D3** | ✅ **F02 genişledi: 3-yönlü süre tutarsızlığı.** Offline analitik (`LocalSessionAnalytics`) süreyi **wall-clock** (`End-Start`) hesaplıyor; sunucu C# `TotalActiveDuration` (pause hariç); SQL View wall-clock. Pause içeren oturumda üçü uyuşmuyor. `Enrich*`/high-water heuristics, "0dk → zıpla" davranışının kök nedenini örtmeye çalışıyor. F02 kapsamı offline yolu da içerecek. |
| **D4** | ✅ İki notification migration'ı idempotent (`COL_LENGTH` kontrolü) — şema ↔ entity tutarlı. Sadece "iki migration aynı işi yapıyor" temizlik notu (→ F43 kapsamına). |
| **D5** | ✅ `CreateTaskDtoValidator` **hiç yok** (→ F01'e dahil). `CreateTaskDto.Status` alanı controller'da kullanılmıyor — **ölü/yanıltıcı alan** (→ F40/cleanup). 3 validator de tetiklenmiyor (F01 doğrulandı). |

**Faz 0 çıktısı yeni görevler:** F44 (async void → güvenli handler), F45 (Dashboard error state) Faz 3'e eklendi.

---

## Faz 1 — Kritik / Güvenlik / Veri Bütünlüğü

| ID | Bulgu | Yaklaşım | Hedef dosya | Efor/Risk |
|---|---|---|---|---|
| F01 | FluentValidation çalışmıyor | `SharpGrip.FluentValidation.AutoValidation.Mvc` + `AddFluentValidationAutoValidation()`; eksik `CreateTaskDtoValidator`, `RegisterRequestDtoValidator` | Program.cs, Validators/** | M / Orta |
| F16/F27 | Notification over-posting | `CreateNotificationDto`; `UserId/IsDeleted/IsRead` body'den alınmaz | NotificationController.cs | S / Düşük |
| F18/F28 | `ex.Message` sızıntısı + global handler yok | `UseExceptionHandler` + `ProblemDetails`; generic catch desenini kaldır | Tüm controller'lar, Program.cs | M / Orta |
| F19/F29 | Rate limiting yok | `AddRateLimiter`; login/register/refresh | Program.cs, AuthController.cs | M / Düşük |
| F20 | Hardcoded JWT secret 3 yerde | Tek `JwtTokenService`/options; dev secret user-secrets'e | Program.cs, AuthService.cs | M / Orta |
| F02 | View(wall-clock) ↔ C#(TotalActiveDuration) süre | DB'de saklanan süreyi tek kaynak yap; view'ı yeniden yaz + migration | View migration, StudySession.cs | L / Yüksek |
| F03 | Soft-delete ders → istatistik kaybı (D1 kırmızıysa) | `IgnoreQueryFilters()` ile lesson join veya ayrı sorgu | StudySessionRepository.cs | M / Orta |
| F31 | Refresh token düz metin | SHA-256 hash'le sakla + karşılaştır | AuthService.cs, AppUser.cs + migration | M / Orta |
| F30 | Register'da kullanıcı enumerasyonu | Duplicate'te generic mesaj | AuthService.cs | S / Düşük |

### Faz 1 — İlerleme

| ID | Durum | Not |
|---|---|---|
| **F16/F27** | ✅ **Tamam** | `CreateNotificationDto`/`NotificationDto` eklendi; controller artık entity yerine DTO alıyor, `UserId/IsRead/IsDeleted` istemciden alınmıyor (sunucu/context atıyor), yanıt entity sızdırmıyor. Test: `NotificationOverPostingTests` (2). |
| **F01** | ✅ **Tamam** | 3. parti paket yerine `FluentValidationActionFilter` (Program.cs'e bağlandı) kayıtlı `IValidator<T>`'leri otomatik çalıştırıyor. Eksik validator'lar eklendi: `CreateTaskDtoValidator`, `RegisterRequestDtoValidator`, `LoginRequestDtoValidator`, `CreateNotificationDtoValidator`. Test: `FluentValidationWiringTests` (4). |
| **F03** | ✅ **Tamam** | `StudySessionRepository.SessionsWithLessonScoped()` global filter'ı bypass edip izolasyonu manuel uyguluyor → silinmiş ders oturumu düşürmüyor, dersin verisi yine geliyor. D1 testi `Assert.Empty`→`Assert.Single` çevrildi (regresyon). |
| **F18/F28** | ✅ **Tamam** | `GlobalExceptionHandler` (IExceptionHandler) + `AddProblemDetails` + `UseExceptionHandler`. İstisna→HTTP eşlemesi (DataConflict→409, KeyNotFound→404, Invalid/Argument→400, Unauthorized→401), beklenmeyen→500 GENERİK (iç mesaj sızmaz) + loglanır. Task/StudySession controller'larındaki `ex.Message` sızdıran try/catch'ler kaldırıldı. Test: `GlobalExceptionHandlerTests` (5). |
| **F19/F29** | ✅ **Tamam** | `AddRateLimiter` + `UseRateLimiter`; `[EnableRateLimiting("auth")]` → AuthController, IP başına 10 istek/dk, aşımda 429. (Otomatik test entegrasyon altyapısı gerektirdiği için eklenmedi; yapılandırma + manuel doğrulanır.) |
| **F20** | ✅ **Tamam** | `JwtSettings` + `JwtTokenService` ile token üretimi/doğrulaması tek noktada; hardcoded dev secret 3 yerden → **tek yere** (Program.cs constant) indi. AuthService `IConfiguration` ve hardcoded secret bağımlılığından kurtuldu; `ExpiryMinutes` invariant-culture parse. Test: `JwtTokenServiceTests` (3). |
| **F30** | ✅ **Tamam** | AuthController.Register artık Identity hata detaylarını/`ex.Message`'ı döndürmüyor; tüm kayıt hataları için tek, ayırt edilemez mesaj → kullanıcı enumerasyonu + leak kapandı. |
| **F31** | ✅ **Tamam** | Refresh token DB'de düz metin yerine **SHA-256 hash** olarak saklanıyor (`JwtTokenService.HashToken`); istemciye ham token döner, karşılaştırma hash üzerinden. Şema değişikliği gerekmez (mevcut oturumlar bir kez yeniden giriş ister). Test: `JwtTokenServiceTests.HashToken...`. |
| **F02a** | ✅ **Tamam (server)** | Tek doğruluk kaynağı = `StudySession.CurrentDuration` (pause hariç + aktif segment). Migration `AlignDashboardViewWithActiveDuration`: `v_DashboardSummary` artık wall-clock yerine `TotalActiveDuration + canlı segment` ile hesaplıyor → Workspace kartı ile grafikler tutarlı. `ProductivityCalculator` da `TotalActiveDuration`→`CurrentDuration`'a hizalandı. **Uyarı:** View SQL Server gerektirdiğinden bu ortamda çalıştırılamadı; `dotnet ef database update` ile uygulanıp doğrulanmalı. |
| **F02b** | ⏳ **Follow-up** | Offline yol (`LocalSessionAnalytics`/`SyncedStudySessionApiService` cache) süreyi wall-clock (`End-Start`) hesaplıyor. Offline pause yakalanmadığı için offline-origin oturumlarda wall-clock=aktif (tutarlı); yalnız **online iken pause edilip cache'lenen** oturumlarda sapma kalır. Tam çözüm timer→cache'e aktif süreyi taşımayı gerektirir (GlobalTimerService + SyncedStudySessionApiService + LocalSessionAnalytics). Ayrı/odaklı efor olarak işaretlendi. |

**Durum:** Tüm testler yeşil — **36/36**. Checkpoint commit: `348f092` (F18/F19/F20). **Faz 1 (server-side) tamamlandı** — kalan tek açık öğe: F02b (offline süre, follow-up).

---

## Faz 2 — Mantık & Tutarlılık

| ID | Bulgu | Yaklaşım | Hedef dosya | Efor |
|---|---|---|---|---|
| F04/F25 | today-total mola/süre/gün | `IsBreak` filtrele, `CurrentDuration`, UTC gün | StudySessionService.cs | S |
| F06/F24 | Aktif oturum race → 500 | `DbUpdateException` → `Conflict(ACTIVE_SESSION_EXISTS)` | StudySessionService.cs, Controller | S |
| F05/F23 | Completed reopen + sessiz hata | Reopen'ı genişlet ya da UI'da engelle; boş if/catch kaldır | TaskItem.cs, TaskApiService.cs | M |
| F08 | Notification cleanup hard-delete + DateTime.Now | Soft-delete + UtcNow | NotificationRepository.cs | S |
| F07 | View TodayStudyMinutes UTC vs local | F11'e bağla; kullanılmıyorsa kaldır | View migration | S |
| F11 | Timezone stratejisi | Client'tan UTC offset; sunucu today/local hesabını offset ile yap | Dashboard/Statistics, repolar | L / Yüksek |
| F17 | GetNotifications entity döndürüyor | `NotificationDto` projeksiyon | NotificationController.cs | S |
| F21 | StatisticsController authorize/range | Explicit `[Authorize]`; range enum | StatisticsController.cs | S |
| F26 | İstemci hata gövdesini Contains ile eşliyor | `code` alanından parse et | AuthService(client), LessonApiService.cs | M |
| F46 | İstemci `DELETE api/notification/all` çağırıyor ama backend'de endpoint yok → 404 (sessizce yutuluyor) | Backend'e `DELETE api/notification/all` (soft-delete) ekle veya client'ı mevcut `cleanup`'a yönelt | NotificationController.cs, NotificationApiService.cs | S |

### Faz 2 — İlerleme

| ID | Durum | Not |
|---|---|---|
| **F17** | ✅ Tamam | GetNotifications artık `NotificationDto`'ya projeksiyon yapıyor (UserId/IsDeleted sızmıyor). |
| **F21** | ✅ Tamam | StatisticsController'a explicit `[Authorize]`; range `switch` ile tek yerde. |
| **F08** | ✅ Tamam | Notification cleanup hard-delete → **soft-delete + UtcNow**. Test: `NotificationCleanupTests`. |
| **F46** | ✅ Tamam | `DELETE api/notification/all` endpoint'i + repo `DeleteAllAsync` (user-scoped soft-delete). |
| **F05/F23** | ✅ Tamam (backend) | `TaskItem.Reopen` artık tamamlanmış görevi de Pending'e alıyor → "tamamlandı"yı geri alma çalışıyor. Test: `TaskItemReopenTests` (3). İstemcideki sessiz yutma temizliği = **F12 (Faz 3)**. |
| **F04/F25** | ✅ Tamam | today-total mola hariç + `CurrentDuration` + UTC gün. Test: `TodayTotalTests`. |
| **F06/F24** | ✅ Tamam | StudySessionController.Start `DbUpdateException` (unique index ihlali) → **409**. Test: `ActiveSessionConstraintTests` (SQLite). |
| **F07** | ⏳ | View `TodayStudyMinutes` UTC vs local — **F11**'e bağlı (timezone stratejisi). |
| **F11** | ⏳ | Timezone stratejisi — cross-cutting + istemci offset header gerektirir (büyük). |
| **F26** | ⏳ | İstemci hata gövdesi `code` parse — **istemci (MAUI) değişikliği**. |

**Durum:** Tüm testler yeşil — **44/44**.

---

## Faz 3 — Frontend / UX / Validation

| ID | Bulgu | Yaklaşım | Hedef dosya | Efor |
|---|---|---|---|---|
| F09/F15 | Login validation yok + buton invalid'de aktif | DataAnnotation + ValidationMessage + EditContext.Validate() | Login.razor | S |
| F10 | Register validation + parola tekrar/güç | DataAnnotation + `[Compare]` + min uzunluk | Register.razor | M |
| F13 | LoadCalendar error/empty state yok | try/catch + hata + boş-durum | TasksBase.cs | S |
| F12 | UpdateTaskStatusAsync sessiz yutma | Hata yüzeye çıksın (F05 ile) | TaskApiService.cs | S |
| F14 | Timer async handler unobserved exception | Handler içi try/catch + hata event'i | GlobalTimerService.cs | S |
| F44 | async void event handler'larda unobserved exception | `OnTimerFinished`, debounce `Elapsed`, `SaveNotesAuto`'ya try/catch + hata yüzeyi | Workspace.razor | S |
| F45 | Dashboard boş `catch {}` → error state yok | `SoftReload/LoadAsync`'te hata yakalanıp kullanıcıya gösterilsin (retry) | DashboardBase.cs | S |

### Faz 3 — İlerleme (DesktopClient Windows hedefinde derlenerek doğrulandı)

> Not: MAUI DesktopClient bu ortamda **Windows hedefinde derleniyor** (~15-25 sn, 0 hata),
> yani client değişiklikleri derleme ile doğrulanabiliyor. (Runtime/UI testi hâlâ uygulamayı
> çalıştırmayı gerektirir.)

| ID | Durum | Not |
|---|---|---|
| **F09/F15** | ✅ Tamam | Login.razor: `LoginViewModel`'e `[Required]`/`[EmailAddress]` + `ValidationMessage`. Boş/geçersiz submit `OnValidSubmit` ile engelleniyor. |
| **F10** | ✅ Tamam | Register.razor: DataAnnotation + **şifre tekrar** alanı (`[Compare]`) + `MinLength(6)` + ValidationMessage. |
| **F13** | ✅ Tamam | TasksBase.LoadCalendar try/catch/finally + `loadError` → hung spinner / unhandled exception önlendi. |
| **F14** | ✅ Tamam | GlobalTimerService timer `Elapsed` async handler'ı try/catch içine alındı → gözlemlenmeyen exception app'i çökertmiyor. |
| **F45** | ✅ Tamam | DashboardBase: sessiz `catch {}` → loglama; ilk yüklemede her iki kaynak da başarısızsa `loadError`. |
| **F12** | ⏳ | TaskApiService sessiz yutma — F05 ile çekirdek bug çözüldü; temizlik için sync call-chain doğrulaması gerekiyor. |
| **F44** | ⏳ | Workspace.razor async void — dosya dış kaynaklı değişiklik içerdiğinden çakışmamak için bekletildi. |

---

## Kullanıcı Bildirimli Buglar (B1/B2) — ✅ Düzeltildi

| ID | Bug | Kök neden | Çözüm |
|---|---|---|---|
| **B1** | POST'ta (ör. ders ekleme) **aralıklı 401** / refresh sisteminin bozulması | Token süresi dolup 401 gelince handler refresh + isteği **klonlayarak** retry ediyor; POST gövdesi ilk gönderimde tüketildiğinden klon retry'da boş/bozuk gidiyor → retry 401 kalıyor → logout. Yalnız POST + yalnız token-yenileme anında ("bazen"). | [AuthorizationMessageHandler](StudyTime.DesktopClient/Services/AuthorizationMessageHandler.cs): ilk gönderimden önce `request.Content.LoadIntoBufferAsync()` ile gövdeyi tampona al → klon retry güvenilir. |
| **B2** | **Top 5 görev grafiği boş** (mobil + desktop) | Grafik yalnız **tamamlanmış VE TaskId ile başlatılmış** oturuma sahip görevleri gösteriyordu; oturumlar nadiren task'a bağlandığından kesişim genelde boş. Ayrıca offline'da temp→server task-id reconciliation eşleşmeyi koparıyordu. | Server ([StatisticsService](StudyTime.Application/Services/StatisticsService.cs)) + offline ([LocalSessionAnalytics](StudyTime.DesktopClient/Offline/LocalSessionAnalytics.cs)): "tamamlanmış" şartı kaldırıldı → **süreye göre** sıralanıyor; offline'da TaskId temp→server eşlemesi uygulanıyor. UI rozeti "Süreye göre", satır ikonu tamamlanma durumuna göre. Test: `TopTasksStatisticsTests`. |

---

## Faz 4 — Performans

| ID | Bulgu | Yaklaşım | Hedef dosya | Efor |
|---|---|---|---|---|
| F22/F33 | Her istekte DB'den kullanıcı çekme | HWID'yi JWT claim'ine göm; filter claim'i kontrol etsin | ActiveSessionFilter.cs, AuthService.cs | M |
| F34 | GetTasksByLessonIdAsync over-fetch | Sunucu tarafı filtre/endpoint | TaskApiService.cs | S |
| F37 | GetAllTasks sayfalama yok | Paged `GetTasksAsync`'e geçir | TaskController.cs, TaskService.cs | M |
| F36 | SARGable olmayan tarih sorguları | `>= start && < end.AddDays(1)`; index | TaskRepository.cs | M |
| F35 | Dashboard çoklu sorgu | View'dan yararlan, redundant turları azalt | DashboardService.cs | M |

---

## Faz 5 — Kod Kalitesi & Temizlik

| ID | Bulgu | Yaklaşım | Hedef | Efor |
|---|---|---|---|---|
| F38 | Tekrar eden optimistic-update deseni | `ApplyOptimisticUpdate` yardımcısı | Task/Lesson/StudySession service | M |
| F39 | Ölü `HttpClient` kaydı | Kullanılmayan registration'ı sil | MauiProgram.cs | S |
| F40 | Ölü validator'lar | F01 ile aktifleşir; kalanı temizle | Validators/** | S |
| F41 | Console.WriteLine loglama | `ILogger`'a çevir | TaskController.cs, LessonApiService.cs | S |
| F42 | Emoji/karışık dil yorumlar | Sadeleştir | Geneli | S |
| F43 | Kullanılmayan hard-delete repo metodları | Kaldır veya `[Obsolete]` | *Repository.cs | S |
| F32 | DEBUG SSL bypass | Sadece `#if DEBUG` içinde olduğunu teyit | MauiProgram.cs | S |

---

## Test & Regresyon Stratejisi

- **Backend birim/entegrasyon:** F01, F02, F03 (D1), F06, F16, F18, F19, F31.
- **Domain testleri:** `TaskItem` state machine (F05), `StudySession` pause/resume süre (F02).
- **Manuel/UX:** Login/Register validation (F09/F10), takvim error-empty (F13), timer hata (F14).
- **Regresyon kapısı:** Her faz sonunda `dotnet build` + `dotnet test` yeşil olmadan
  sonraki faza geçilmez.

---

## İzlenebilirlik Matrisi (her bulgu → görev)

| Review bölümü | Bulgu | Görev | Faz |
|---|---|---|---|
| Kritik 1 | FluentValidation çalışmıyor | F01 | 1 |
| Kritik 2 | View↔C# süre | F02 | 1 |
| Kritik 3 | Soft-delete istatistik kaybı | D1→F03 | 0/1 |
| Mantık 1 | today-total mola/süre/gün | F04 | 2 |
| Mantık 2 | Completed reopen + sessiz hata | F05/F12 | 2/3 |
| Mantık 3 | Aktif oturum race 500 | F06 | 2 |
| Mantık 4 | View TodayStudyMinutes UTC | F07 | 2 |
| Mantık 5 | Notification cleanup hard-delete | F08 | 2 |
| Frontend | Login validation | F09 | 3 |
| Frontend | Register validation/parola | F10 | 3 |
| Frontend | over-fetch GetTasksByLessonId | F11/F34 | 4 |
| Frontend | UpdateTaskStatus sessiz yutma | F12 | 3 |
| Frontend | LoadCalendar error/empty | F13 | 3 |
| Frontend | Timer unobserved exception | F14 | 3 |
| Frontend | Buton invalid'de aktif | F15 | 3 |
| Backend | Notification over-posting | F16 | 1 |
| Backend | GetNotifications entity sızıntı | F17 | 2 |
| Backend | ex.Message sızıntısı/global handler | F18 | 1 |
| Backend | Rate limiting | F19 | 1 |
| Backend | Hardcoded JWT secret | F20 | 1 |
| Backend | StatisticsController authorize/range | F21 | 2 |
| Backend | ActiveSessionFilter DB hit | F22/F33 | 4 |
| FE-BE | reopen semantik | F05/F23 | 2 |
| FE-BE | start 500 vs 409 | F06/F24 | 2 |
| FE-BE | today-total iki kaynak | F04/F25 | 2 |
| FE-BE | hata gövdesi string match | F26 | 2 |
| Güvenlik | over-posting | F16/F27 | 1 |
| Güvenlik | hata sızıntısı | F18/F28 | 1 |
| Güvenlik | rate limit | F19/F29 | 1 |
| Güvenlik | register enumeration | F30 | 1 |
| Güvenlik | refresh token düz metin | F31 | 1 |
| Güvenlik | DEBUG SSL bypass | F32 | 5 |
| Performans | filter DB | F22/F33 | 4 |
| Performans | over-fetch | F34 | 4 |
| Performans | dashboard çoklu sorgu | F35 | 4 |
| Performans | SARGable tarih | F36 | 4 |
| Performans | GetAllTasks sayfalama | F37 | 4 |
| Kalite | tekrar eden desen | F38 | 5 |
| Kalite | ölü HttpClient | F39 | 5 |
| Kalite | ölü validator | F40 | 5 |
| Kalite | Console.WriteLine | F41 | 5 |
| Kalite | emoji/dil yorumlar | F42 | 5 |
| Kalite | kullanılmayan hard-delete | F43 | 5 |
| Görmem gereken | UI/state/leak/XSS | D2 ✅ (leak/XSS yok; F44/F45 doğdu) | 0 |
| Görmem gereken | offline caches | D3 ✅ (F02 3-yönlü oldu) | 0 |
| Görmem gereken | migration tutarlılık | D4 ✅ (tutarlı) | 0 |
| Görmem gereken | DTO/validation | D5 ✅ (CreateTaskDtoValidator yok) | 0 |
| Faz 0 doğurdu | async void unobserved exception | F44 | 3 |
| Faz 0 doğurdu | Dashboard error state yok | F45 | 3 |
| Faz 1 doğurdu | `DELETE notification/all` endpoint yok (404) | F46 | 2 |
| Faz 1 doğurdu | Offline süre wall-clock (aktif-süre değil) | F02b | follow-up |

**Toplam:** 5 keşif (D1–D5, tamamlandı) + 45 düzeltme görevi → review'daki her madde kapsandı.

---

## Tahmini Süre

- Faz 0: ~0.5–1 gün · Faz 1: ~3–4 gün · Faz 2: ~2 gün
- Faz 3: ~1.5 gün · Faz 4: ~2 gün · Faz 5: ~1 gün
