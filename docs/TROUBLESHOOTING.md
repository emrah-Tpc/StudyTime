# Sorun Giderme ve Karşılaşılan Hatalar (Troubleshooting)

Bu belge, geliştirme süreci boyunca karşılaşılan spesifik ve tekrar edebilecek kritik hataların çözümlerini barındırmak amacıyla oluşturulmuştur.

## 1. "E-posta veya şifre hatalı" Hatası (Doğru girilmesine rağmen)
**Sorun:**
Masaüstü veya mobil uygulamada doğru e-posta ve şifre girilmesine rağmen, API `401 Unauthorized` dönüyor ve arayüzde "E-posta veya şifre hatalı." uyarısı gösteriliyordu.

**Nedeni:**
Giriş işlemi arka planda (backend) başarılı olduktan sonra JWT Token üretme aşamasında API, `appsettings.json` içinden `JwtSettings:Secret` anahtarını okuyordu. Ancak güvenlik geliştirmeleri sırasında (Görev A1) bu değer boş (`""`) bırakılarak ortam değişkenine (Environment Variable) taşınmıştı.
`AuthService.cs` içindeki Token üretim kodu bu durumu yakalayamadığı için `Encoding.UTF8.GetBytes(secret)` metodu boş bir anahtar üretiyor ve sistem şu hatayı fırlatıyordu:
`IDX10703: Cannot create a 'Microsoft.IdentityModel.Tokens.SymmetricSecurityKey', key length is zero.`
Bu hata dışarıya "Geçersiz e-posta veya şifre" / `Unauthorized` olarak yansıyordu.

**Çözüm:**
`StudyTime.Application\Services\AuthService.cs` dosyasındaki token üretim algoritması, `Program.cs`'deki (uygulama başlangıcı) anahtar okuma stratejisiyle aynı olacak şekilde güncellendi. Eğer `appsettings.json` içerisinde değer bulunamazsa, sistemin çökmek yerine geliştirme ortamı için geçici bir "fallback" şifresi (`DevelopmentSuperSecretKey...`) kullanması sağlandı. Üretim ortamında (Prod) ise bu değer `JWT_SECRET` ortam değişkeninden otomatik alınacaktır.
