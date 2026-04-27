# StudyTime — Windows kurulumu (yayımlanmış sürüm)

Bu belge, `dotnet publish` ile üretilen **self-contained** Windows x64 paketinin son kullanıcı veya test makinesine nasıl kurulacağını açıklar.

## Gereksinimler

- **İşletim sistemi:** Windows 10 sürüm 1809 veya üzeri (veya Windows 11), **64 bit**.
- **WebView2:** Arayüz Blazor WebView kullanır. Çoğu Windows 10/11 kurulumunda [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) zaten yüklüdür. Uygulama açılmazsa veya beyaz ekran görürseniz WebView2’yi yükleyin veya onarın.
- **Ağ:** Sunucu API’sine bağlanacaksanız makinenin ilgili adrese erişebildiğinden emin olun (güvenlik duvarı / VPN).

## Paket nerede?

Geliştirme makinesinde publish sonrası örnek yol:

`StudyTime.DesktopClient\bin\Release\net9.0-windows10.0.19041.0\win10-x64\publish\`

Bu klasörün **tamamı** bir arada dağıtılmalıdır; yalnızca `.exe` dosyasını kopyalamak yeterli değildir.

## Kurulum adımları

1. `publish` klasörünün içeriğini hedef bilgisayarda bir klasöre kopyalayın (ör. `C:\Program Files\StudyTime\` veya masaüstünde `StudyTime` klasörü).
2. İsterseniz tüm klasörü ZIP ile sıkıştırıp taşıyın; açıldığında yine **aynı klasördeki tüm dosyalar** birlikte kalmalıdır.
3. **`StudyTime.DesktopClient.exe`** dosyasına çift tıklayarak çalıştırın.
4. İlk çalıştırmada Windows “bilinmeyen yayıncı” uyarısı verebilir; kaynağa güveniyorsanız “Yine de çalıştır” / “Daha fazla bilgi” üzerinden onaylayın.

> **Not:** Bu çıktı “unpackaged” (MSIX kurulum sihirbazı olmadan) dağıtım içindir. İsterseniz kısayol oluşturup Başlat menüsüne veya masaüstüne sabitleyebilirsiniz.

## Yapılandırma (API adresi)

Uygulama ile birlikte gelen `appsettings.json` (veya gömülü eşdeğeri) içinde API tabanı (`StudyTime:ApiBaseUrl`) tanımlıdır. Sunucu adresiniz farklıysa, dağıtımdan önce projede bu değeri güncelleyip **yeniden publish** alın veya hedef makinede yapılandırma yönteminize göre düzenleyin (unpackaged senaryoda genelde yeniden derleme gerekir).

## Veriler nerede saklanır?

Yerel SQLite ve önbellek genelde kullanıcıya özel uygulama veri klasöründe tutulur (MAUI `FileSystem.AppDataDirectory`). Tam yol sürüme ve paket kimliğine göre değişebilir; tam sıfırlama gerektiğinde uygulamayı kaldırıp bu veri klasörünü temizlemek gerekir (ileri kullanıcı işlemi).

## Sorun giderme

| Sorun | Olası çözüm |
|--------|----------------|
| Pencere açılmıyor / boş ekran | WebView2 Runtime kurun veya güncelleyin. |
| API’ye bağlanamıyor | `ApiBaseUrl`, firewall, sertifika (HTTPS) ve sunucunun çalıştığını kontrol edin. |
| Eksik DLL / hata | `publish` klasörünün eksiksiz kopyalandığından emin olun; tek exe değil tüm dosyalar gerekir. |

## Yeniden oluşturma (geliştirici)

Depo kökünden:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-WindowsSelfContained.ps1
```

Çıktı yine `...\win10-x64\publish\` altında üretilir.
