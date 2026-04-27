# StudyTime — Yayına Giriş Yol Haritası (Release Roadmap)

Bu belge, StudyTime projesinin hem **Mobil (Android/iOS)** hem de **Desktop (Windows)** için canlıya çıkış sürecinde geçmesi gereken teknik ve süreçsel tüm adımları tanımlar.

---

## 📌 Aşama 1: Temel Özelliklerin Tamamlanması (Kodun Kilitlenmesi)
*Uygulamanın mantıksal eksiklerinin tamamlanıp tam bir versiyona ulaşması.*

- [ ] **Mevcut 9 Günlük Aksiyon Planını Bitirme**: `DAILY_ACTION_PLAN.md` içindeki kritik güvenlik, JWT yapılandırmaları ve offline Outbox eksiklerinin çözülmesi.
- [ ] **Gerçek Ortam (Prod) API'sine Geçiş**: Sunucu/Backend bileşenlerinin canlı sunucu (VPS/Cloud) üzerine deploy edilmesi, CORS ve HTTPS yapılandırmalarının tamamlanması.
- [ ] **Code Freeze (Kod Dondurma)**: Tüm ana özelliklerin eklenmesinin durdurulup sadece hata çözümleme (bugfix) aşamasına geçilmesi.

---

## 📌 Aşama 2: Platform Özelleştirmeleri (UI/UX ve Config)
*Mobil cihazlar ve Masaüstü uygulama deneyimini doğal hissettirecek dokunuşlar.*

- [ ] **Grafik varlıkları**: Windows, Android ve iOS için ortak **Uygulama İkonu (App Icon)** ve **Açılış Ekranı (Splash Screen)** tasarımlarının yapılıp MAUI'ye entegre edilmesi.
- [ ] **UI/UX Pürüzleri**: 
  - Mobil için sanal klavye açılınca ekranın bozulmaması (Safe Area).
  - Android "Geri" tuşunun (Back Button) Blazor içinde geri gitme komutu olarak algılanması (Interceptor).
  - Masaüstü için uygulamanın çerçeve tasarımı (Window Frame) ve küçültme/büyütme davranışları.
- [ ] **Platform İzinleri (Permissions)**: Uygulamanın kullandığı dosya kayıt (sqlite), bildirim (notification) vb. senaryolar için Android/iOS Manifest ve Windows Capabilities dosyalarının yapılandırılması.

---

## 📌 Aşama 3: İç Testler ve Kalite Güvencesi (QA & Beta)
*Sistemin kırılma noktalarını test edip yayından önce son rötüşlerin yapılması.*

- [ ] **Simülatör yerine Gerçek Cihaz Testleri**: Windows PC, en az bir Android ve bir iPhone cihazda tamamen baştan sona (Kayıt, Offline oturum, Online oturum) kullanım testi.
- [ ] **Çevrimdışı (Outbox) Stres Testi**: İnterneti kapatıp oturum açıp kapatarak peş peşe senaryolar yaratmak, interneti açtığımızda API'ye doğru senkronizasyonun incelenmesi.
- [ ] **İç Test Dağıtımı (Internal Testing)**: 
  - Android için: Google Play Internal Testing sürümü alma.
  - iOS için: Apple TestFlight (Internal/External testler) dağıtımı.
  - Masaüstü için: MSIX oluşturup, güvenilen bilgisayarlarda denenmesi (Sideloading).

---

## 📌 Aşama 4: Mağaza Hazırlıkları (Pre-Launch & Pazarlama)
*Uygulamanın halka açılmadan önceki kimliğinin ve yasal alt yapısının oluşturulması.*

- [ ] **Yasal Sayfaların Oluşturulması**: Web sitesi (Landing Page) üzerinde **Gizlilik Politikası (Privacy Policy)** ve **Kullanım Koşulları (Terms of Service)** ayarlanması (Aksi taktirde mağazalar reddeder).
- [ ] **Geliştirici Hesaplarının Ayarlanması**: 
  - Google Play Console (Ücretin ödenip hesabın onaylatılması).
  - Apple Developer Account (Yıllık üyelik ve şirket/bireysel onayı).
  - Microsoft Partner Center (Windows platformu için geliştirici).
- [ ] **Mağaza Materyalleri**: Her üç platform için uygun boyutlarda ekran görüntüleri, tanıtım videoları ve uygulamanın meta verilerinin (Açıklama, etiketler vb.) hazırlanması.

---

## 📌 Aşama 5: Yayına Çıkış (Launch 🚀)
*Zorlu sürecin sonu ve mağaza onayları.*

- [ ] **Versiyonlama**: `.csproj` dosyalarında ve MAUI ayarlarında Versiyon No'nun (1.0.0 vb.) ayarlanıp Release derlemesi işlemi (`.aab` Android, `.ipa` iOS, `.msix` Windows).
- [ ] **Uygulama İnceleme Süreci (App Review)**: Ürünlerin uygulama marketlerine Onay'a (Review) gönderilmesi (Özellikle iOS ilk incelemesi süre alabilir).
- [ ] **Canlıya Alma Özeti**: Onaylanan uygulamaların aynı günde görünür yapılması (Publish).

---

## 📌 Aşama 6: Yayın Sonrası (Post-Launch & Monitoring)
*Kullanıcılarla ilk yüzleşme.*

- [ ] **Hata Gözlemcisi (Crash Analytics)**: Beklenmeyen bir mobil kilitlenme veya çökme durumundan log almak (örn. AppCenter, Crashlytics, Sentry) ve bunları izlemek.
- [ ] **Geri Bildirimler**: İlk kullanıcılardan gelen hatalarla V1.0.1 hotfix (acil yama) planlaması.
