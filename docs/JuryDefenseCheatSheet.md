# StudyTime - Jüri Savunma ve Mülakat Rehberi (Cheat Sheet)

GBYF KIBRIS yarışmasında üst düzey jüri (Bakanlık Yetkilileri, Siber Güvenlik Yöneticileri ve Bilişim CEO'ları) için hazırlanan, projenin kaynak kodlarına ve mimari vizyonuna dayanan potansiyel zorlayıcı sorular ve stratejik cevaplar.

---

## 1. Veri Bütünlüğü ve Offline-First Mimarisi

**Zorlayıcı Soru:** 
> "Uygulamanız çevrimdışı (offline) çalışabiliyor. Peki kullanıcı internet yokken cihazında bir veri ekledi/sildi, o sırada bulutta da veri değişti diyelim. Bağlantı geldiğinde bu çakışmayı (Conflict) veya veri tutarsızlığını kod tarafında nasıl çözüyorsun?"

**Mühendislik Cevabı (Teknik):** 
"Sistemimizde `sqlite-net-pcl` kullanarak cihazda yerel bir 'OutboxQueue' (Gidenler Kuyruğu) oluşturduk. İnternet yokken yapılan tüm işlemler (örneğin görev ekleme veya `StudySession` durdurma) bu kuyruğa yazılır. Bağlantı sağlandığında `OutboxProcessor` ve `SyncedStudySessionApiService` katmanları devreye girer. İşlemler senkronize edilirken 'Sunucu Otoritesi' (Server Authority) prensibi ile çalışıyoruz. Yerel Guid'ler ile Sunucu Guid'leri `SessionServerIdMap` ile eşlenir. Eğer zayıf internet nedeniyle cihaz aynı veriyi iki kez gönderirse, API tarafındaki idempotent (tekrara duyarlı) yapımız sayesinde çift kayıt (duplicate) oluşması engellenir. Çakışma (Conflict) durumlarında ise `409 Conflict` hata kodlarını yöneterek veri kaybı olmadan senkronizasyonu tamamlıyoruz."

**CEO / Yönetici Cevabı (C-Level):**
"Mimariyi tasarlarken temel felsefemiz 'Sıfır Veri Kaybı' (Zero Data Loss) oldu. Sistemimizi, internetin kesildiği senaryolarda dahi müşterinin verisini ve emeğini koruyacak bir 'Öncelikle Çevrimdışı' stratejisiyle kurduk. Veriler önce cihazda yerel olarak güvenle tutuluyor, bağlantı geldiğinde merkezi sunucumuz bu kayıtları otomatik ve sessiz bir şekilde ana sisteme işliyor. Müşteriye hiçbir zaman 'veri kaydedilemedi' hatası yaşatmıyoruz; bu pürüzsüz deneyim müşteri sadakatini ve güvenini doğrudan artıran en büyük ticari avantajımızdır."

---

## 2. Siber Güvenlik ve Veri Gizliliği

**Zorlayıcı Soru:** 
> "Uç cihazdaki (Edge) yerel SQLite verileri ve sunucu ile olan API haberleşmeniz (örneğin JWT) siber saldırılara, araya girmelere (Man-In-The-Middle) veya token çalınmasına karşı ne kadar güvenli?"

**Mühendislik Cevabı (Teknik):** 
"Güvenliği birden fazla katmanda (`Defense-in-Depth`) ele alıyoruz. API tarafında tek ve kalıcı bir token vermek yerine, `AuthService.cs` içerisinde yapılandırdığımız 'Access Token + Refresh Token' (Token Rotasyonu) mimarisini kullanıyoruz. Yani çalınsa bile dakikalar içinde ömrü dolan kısa süreli anahtarlar üretiyoruz. Çalınan bir token'ın hacker'ın bilgisayarında çalışmasını engellemek için ise `IDeviceIdentityService` ile kullanıcının cihazına özgü (Anakart/CPU bazlı) bir `X-Hardware-Id` üretiyor ve API'deki `ActiveSessionFilter` ile gelen her isteğin donanım parmak izini doğruluyoruz. Kodumuzda hiçbir hassas şifre barındırmıyoruz; veritabanı şifreleri ve JWT anahtarları (Secrets) işletim sistemi seviyesindeki 'Güvenli Ortam Değişkenlerinde' (Environment Variables) saklanıyor."

**CEO / Yönetici Cevabı (C-Level):**
"Kullanıcı güvenliğini sadece basit bir şifre girişine bırakmadık; bankacılık ve fintech sektöründe gördüğümüz ileri düzey 'Donanım Doğrulama' (Hardware Fingerprinting) ve otomatik anahtar yenileme sistemlerini projeye entegre ettik. Sistem, hesabınıza giriş yapan cihazı fiziksel donanımından tanır. Veriler cihazdan buluta akarken kurumsal düzeyde (Enterprise-Grade) şifrelemeyle korunur. Böylelikle yetkisiz erişim ve veri hırsızlığı riski sıfıra indirilerek yatırımcılara ve kullanıcılara maksimum güven veren kapalı bir ekosistem yaratılmıştır."

---

## 3. Performans ve Optimizasyon

**Zorlayıcı Soru:** 
> "Mobilde React Native veya Flutter varken neden .NET MAUI Blazor Hybrid seçtin? Ayrıca sunucuda karmaşık veri analitiği sorgularını (örneğin Dashboard) RAM'i şişirmeden, sunucuyu yormadan nasıl optimize ediyorsun?"

**Mühendislik Cevabı (Teknik):** 
"MAUI Blazor Hybrid'i tercih etmemizin teknik sebebi, C# iş kurallarını ve HTML/CSS arayüzünü tamamen tek bir kod tabanında (Single Codebase) toplayabilmesidir; böylece Web API ve İstemci aynı dili ve Domain/DTO sınıflarını ortak paylaşır. Sunucu performansına gelirsek; milyonlarca satırı bellek (RAM) üzerinde LINQ ile hesaplamak yerine, yükü `Entity Framework Core 9` aracılığıyla doğrudan SQL Server'a aktarıyoruz. `DashboardRepository` içerisinde yapılandırdığımız `v_DashboardSummary` SQL View'ları ve Global Query Filter'lar sayesinde veriler (Sum, Count vb.) veritabanı motoru düzeyinde (I/O ve Compute optimize edilerek) aggregate edilir ve sunucu RAM'ine sadece işlenmiş ufak JSON paketleri yansır."

**CEO / Yönetici Cevabı (C-Level):**
"Teknoloji seçimlerimizi 'Sürdürülebilirlik, Operasyonel Hız ve Düşük Maliyet (TCO)' vizyonuyla yaptık. Dört farklı platform (Windows, iOS, Android, Mac) için dört ayrı ekip kurup bütçe yakmak yerine, tek bir mühendislik eforuyla tüm cihazlara çıkış yapabilen 'Birleşik Mimarili' hibrit modeli benimsedik. Sunucu tarafında ise milyarlarca kullanıcı verisini işlerken sunucularımızın çökmemesi ve bulut (Cloud) faturalarımızın patlamaması için ağır matematiksel işlemleri veritabanı motoruna ittik. Bu, projemizin yarın milyonlarca kullanıcıya teknik bir darboğaz yaşamadan ölçeklenebileceğinin kanıtıdır."

---

## 4. Veri Analitiği (Altın Saatler / Sliding Window Algoritması)

**Zorlayıcı Soru:** 
> "Uygulamadaki 'En Verimli Saatler' (Peak Productivity / Golden Hours) verisini sadece basit bir gruplamayla mı yapıyorsunuz, bu algoritma arka planda kodsal/matematiksel olarak nasıl çalışıyor?"

**Mühendislik Cevabı (Teknik):** 
"Bunu sadece basit bir 'hangi saat daha çok çalışıldı' SQL gruplamasıyla değil, `StatisticsService.cs` içerisinde uyguladığımız dinamik bir `Sliding Window` (Kayan Pencere) algoritması ile yapıyoruz. Çalışma oturumlarını (mola süreleri hariç tutularak) saatlik dilimlere ayırıyoruz (`minutesByHour` dizisi). Daha sonra döngü ile `minutesByHour[h] + minutesByHour[h+1] + minutesByHour[h+2]` formülünü uygulayarak kesintisiz 3 saatlik bloklar oluşturuyoruz. Bloklar kendi aralarında kıyaslanarak kullanıcının maksimum odaklanabildiği en yüksek puanlı (MaxWindowScore) 'Verimlilik Penceresini' matematiksel olarak tespit ediyor ve arayüze iletiyoruz."

---

## 5. Bildirim Sistemi ve Context-Aware UX (Notification Architecture)

**Zorlayıcı Soru 1 (UX ve Spam Kontrolü):**
> "Uygulama açıkken (masaüstünde veya telefonda çalışırken) hem sağdan Windows balonu/Android Push fırlatıp hem de ortadan Toast mesajı çıkararak kullanıcıyı bildirime boğup darlayacak mısınız?"
**Mühendislik Cevabı (Teknik):** "Hayır, 'Context-Aware UX' (Bağlama Duyarlı Deneyim) prensibini uyguluyoruz. Bildirim fırlatılmadan önce MAUI'nin `App.Current.MainPage.Window` lifecycle event'leri üzerinden uygulamanın Foreground (Ön planda) mu yoksa Background (Arka planda) mu olduğunu kontrol ediyoruz. Eğer uygulama aktifse OS bildirimini (Push/Tray) eziyor (suppress) ve sadece Blazor arayüzüne zarif bir Toast/Snackbar düşürüyoruz. Uygulama simge durumundaysa OS bildirimi fırlatılıyor."
**CEO / Yönetici Cevabı (C-Level):** "Dijital yorgunluğun (Notification Fatigue) önüne geçmek en büyük UX önceliğimizdir. Sistemimiz, kullanıcının o an uygulamayla olan etkileşimine göre en az rahatsız edici iletişim kanalını otomatik seçer. Bu sayede uygulamanın silinme (Churn) oranını düşürüyor ve kullanıcı tutundurmayı (Retention) artırıyoruz."

**Zorlayıcı Soru 2 (Mimari ve Spagetti Kod Riski):**
> "Blazor Web UI içinden Windows System Tray'i veya Android Push altyapısını nasıl tetikliyorsunuz? Tüm bu platform kodları birbirine girip spagetti koda dönüşmüyor mu?"
**Mühendislik Cevabı (Teknik):** "SOLID prensiplerinden 'Dependency Inversion' (Bağımlılığı Tersine Çevirme) kuralını sıkı şekilde uyguluyoruz. Blazor arayüzümüz sadece `IAppNotificationService` interface'ini bilir, platformun ne olduğuyla ilgilenmez. `MauiProgram.cs` içerisinde her platform kendi implementasyonunu (Örn: `WindowsTrayNotificationService` veya `MobileLocalNotificationService`) sisteme enjekte eder. Kodlarımız tamamen izole ve modülerdir."
**CEO / Yönetici Cevabı (C-Level):** "Mimarimiz 'Tak-Çalıştır' (Plug-and-Play) mantığıyla inşa edilmiştir. Yarın uygulamamızı Mac bilgisayarlara veya akıllı saatlere çıkarmak istediğimizde, çekirdek iş kurallarımızı (Business Logic) tek satır bile değiştirmeden sadece o donanıma özel ufak bir eklenti yazarak ilerleyebiliriz. Bu, mühendislik maliyetlerini minimize eder."

**Zorlayıcı Soru 3 (Veri Bütünlüğü ve Offline Durumu):**
> "İnternet koptuğunda veya cihaz kapalıyken atılan bildirimler kayboluyor mu? Başka bir cihaza geçince okuduğum bildirimleri tekrar okumamı mı isteyeceksiniz?"
**Mühendislik Cevabı (Teknik):** "Tüm bildirimlerin 'Single Source of Truth' (Tek Doğruluk Kaynağı) veritabanımızdaki `Notifications` tablosudur. Çevrimdışı atılan bir bildirim doğrudan yerel SQLite veritabanına yazılır. İnternet geldiğinde asenkron olarak arka planda sunucuyla senkronize edilir (Outbox Pattern). Bir cihazda 'okundu' (IsRead=true) olan bildirim, anında diğer tüm cihazlarda da senkronize olur."
**CEO / Yönetici Cevabı (C-Level):** "Müşterilerimize Omnichannel (Çok Kanallı) ve kesintisiz bir deneyim sunuyoruz. Müşteri bir uyarıyı cep telefonunda okuduysa, masaüstüne geçtiğinde o uyarıyla tekrar karşılaşarak vakit kaybetmez. Bilgi sadece tek bir yerde yaşar ve tüm cihazlara mükemmel bir şekilde senkronize olur."

**Zorlayıcı Soru 4 (Veri Gizliliği):**
> "Bu bildirimlerin (özellikle performans ve odaklanma verilerinin) gizliliği cihazdaki SQLite dosyasında nasıl korunuyor? Başka bir uygulama bu dosyaya erişebilir mi?"
**Mühendislik Cevabı (Teknik):** "Mobil platformlarda veriler OS seviyesindeki (iOS/Android App Sandbox) izolasyon ile donanımsal olarak korunur. Windows'ta ise çoklu kullanıcı (Multi-User) güvenliği için veriler kullanıcının kendi izole `%AppData%` dizininde şifreli veya kilitli tutulur. Ayrıca API tarafında bildirimler `UserId` tenant filter'ı ile ayrıştırılır; kimse başkasının bildirimini teknik olarak çekemez."
**CEO / Yönetici Cevabı (C-Level):** "Kullanıcı gizliliği sonradan eklenen bir özellik değil, sistemimizin temel yapıtaşıdır (Privacy by Design). Veriler hem cihaz içinde (Data at rest) hem de buluta giderken (Data in transit) kurumsal standartlarda korunarak uluslararası veri güvenliği normlarına tam uyum sağlar."

**Zorlayıcı Soru 5 (Analitik Tutundurma ve Akıllı Tetikleyiciler):**
> "Uygulamanız 'Altın Saatler yaklaşıyor' diye bildirim atıyor. Bu sadece statik bir saat alarmı mı yoksa arkasında gerçek bir akıl var mı?"
**Mühendislik Cevabı (Teknik):** "Kesinlikle statik değil. `StatisticsService` içerisindeki 'Sliding Window' (Kayan Pencere) algoritması kullanıcının son 14 günlük çalışma alışkanlıklarını analiz eder. Puanlama (Scoring) sisteminden en yüksek çıkan 'Peak Productivity' bloğunu tespit eder. Sistem, kullanıcının o verimli saatinden 15 dakika önce lokal bir arkaplan tetikleyicisi oluşturur ve sadece o an boşta ise bildirim atar."
**CEO / Yönetici Cevabı (C-Level):** "Biz kullanıcıyı rastgele saatlerde uyarıp rahatsız etmiyoruz; kendi tarihsel verisine dayanarak, beyninin odaklanmaya en hazır olduğu anı matematiksel olarak bulup onu derse davet ediyoruz. Bu 'Veri Odaklı (Data-Driven) Tutundurma' stratejisi, uygulamamızı bir kronometre olmaktan çıkarıp kişisel bir AI asistanına dönüştürmektedir."

---

## 6. Mülakata Hazırlık: Kavram Sözlüğü ve Ekstra Bilgiler

*(Not: Mülakatta bu terimlerin anlamını bilmeniz, konuya ne kadar hakim olduğunuzu gösterecektir)*

*   **Güvenli Ortam Değişkenleri (Environment Variables) Nedir?**
    *   **Ne İşe Yarar:** Yazılım kodlarının içine veritabanı şifresi veya güvenlik anahtarı (JWT Secret) gibi gizli bilgileri **asla** düz metin (hardcoded) olarak yazmamalıyız. Eğer kodlar GitHub'a yüklenirse bu şifreler herkes tarafından çalınabilir.
    *   **Nasıl Çalışır:** Bunun yerine bu şifreler doğrudan uygulamayı çalıştırdığımız işletim sisteminin (veya bulut sunucusunun) hafızasında "Ortam Değişkeni" olarak saklanır. Uygulama çalışırken bu şifreleri işletim sisteminden okur. Böylece kodunuzu kim incelerse incelesin şifreleri göremez. "Sırlarımız (Secrets) kodda değil, ortam değişkenlerinde" lafı tam olarak bu siber güvenlik standardını anlatır.

*   **Access Token + Refresh Token (Token Rotasyonu) Nedir?**
    *   Eğer kullanıcıya "Seni 1 yıl boyunca hatırlayacağım" deyip 1 yıllık uzun ömürlü tek bir Token verirseniz, o token bir kafedeki Wi-Fi üzerinden çalındığında hacker 1 yıl boyunca sistemi kullanabilir.
    *   Bunun yerine sisteme **Token Rotasyonu** kurduk. Kullanıcıya 15 dakikalık kısa ömürlü bir "Access Token" (Giriş Kartı) ve arka planda saklanan güvenli bir "Refresh Token" verilir. 15 dakika dolduğunda Access Token ölür. Cihaz arka planda kullanıcıya hissettirmeden Refresh Token ile yeni bir Access Token alır. Böylece hacker token çalsa bile 15 dakika içinde elinde patlar.

*   **Hardware Fingerprinting (Donanım Parmak İzi - X-Hardware-Id) Nedir?**
    *   Biri şifrenizi bulsa veya Token'ınızı çalsa bile, sisteme girdiğinde bizim API'miz (Sunucu) şunu sorar: *"Bu hesap genellikle Asus anakartlı ve Intel CPU'lu bir bilgisayardan giriyordu. Şu an gelen istek Apple bir telefondan, cihaz uyuşmuyor!"* 
    *   Cihazın donanım parçalarından kriptografik bir ID üreterek bunu API'ye gönderiyoruz. Bu güvenlik önlemi genellikle yüksek güvenlikli bankacılık sistemlerinde kullanılır. Jürideki Siber Güvenlik uzmanının en çok takdir edeceği noktalardan biri budur.

*   **Idempotent (Tekrara Duyarlı / Tekrarlanabilir) Senkronizasyon Nedir?**
    *   Telefonunuz çevrimdışıyken bir görev oluşturdunuz. İnternet geldiğinde telefon bunu sunucuya yolladı. Ama o sırada internet 1 saniyeliğine koptu. Telefon paketin gidip gitmediğinden emin olamadığı için aynı veriyi tekrar yollar.
    *   Eğer sistem "Idempotent" (Tekrarlanabilir) değilse, sunucuda aynı görevden 2 tane (duplicate) oluşur. İdemponent sistemler aynı isteğin 100 defa gelse bile sadece ilkini işleyip diğerlerini yoksaymasını (veya güncel olana göre işlem yapmasını) sağlayan gelişmiş bir çakışma çözme mantığıdır.

*   **I/O ve Compute Optimizasyonu (RAM'i şişirmemek) Nedir?**
    *   Veritabanınızda kullanıcının 1 milyon satırlık çalışma geçmişi olsun. C# tarafında "Bugün kaç saat çalıştı?" hesabı yapmak için bu 1 milyon satırı sunucunun RAM'ine çekip C# ile toplamak sunucuyu kilitler (RAM şişmesi). 
    *   Biz bunun yerine Entity Framework Core ve SQL Views kullanarak "hesaplama işini" veritabanı motoruna (SQL Server'a) yaptırıyoruz. Veritabanı bunu ışık hızında hesaplayıp C# tarafına sadece sonuç olan "120 dakika" bilgisini gönderiyor. Buna Compute (Hesaplama) ve I/O (Girdi/Çıktı) optimizasyonu denir. Ölçeklenebilirliğin (Scalability) altın kuralıdır.
