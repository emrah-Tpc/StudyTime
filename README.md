# StudyTime

Çalışma süresi ve görev takibi odaklı çözüm: ASP.NET Core API, Entity Framework Core, Identity + JWT, MAUI Blazor masaüstü istemci ve çevrimdışı senkron katmanı.

## Dokümantasyon

- **[Yol haritası ve iyileştirme checklist’i](docs/ROADMAP.md)** — iş akışı özeti, önceliklendirilmiş maddeler (`- [ ]` / `- [x]`), her madde için neden / avantaj / mevcut risk özeti.

## Çözüm yapısı

| Proje | Açıklama |
|--------|----------|
| `StudyTime` | Web API |
| `StudyTime.Application` | Uygulama servisleri ve DTO’lar |
| `StudyTime.Domain` | Varlıklar ve domain kuralları |
| `StudyTime.Infrastructure` | EF Core, migration’lar, repository’ler |
| `StudyTime.DesktopClient` | MAUI Blazor istemci |

Detaylı teknik borç ve güvenlik maddeleri için yukarıdaki `ROADMAP.md` dosyasına bakın.
