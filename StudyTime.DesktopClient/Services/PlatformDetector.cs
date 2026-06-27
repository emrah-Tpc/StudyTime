using Microsoft.Maui.Devices;

namespace StudyTime.DesktopClient.Services;

public class PlatformDetector : IPlatformDetector
{
    // DEV override: Windows uzerinde mobil layout'u test edebilmek icin.
    //   STUDYTIME_FORCE=mobile   -> her zaman mobil layout
    //   STUDYTIME_FORCE=desktop  -> her zaman masaustu layout
    // Tanimsizsa davranis degismez (gercek cihaz idiom'u kullanilir). Uretimde kapali.
    private static readonly string? _force =
        Environment.GetEnvironmentVariable("STUDYTIME_FORCE")?.Trim().ToLowerInvariant();

    public bool IsDesktop =>
        _force == "desktop" ||
        (_force != "mobile" && DeviceInfo.Current.Idiom == DeviceIdiom.Desktop);

    public bool IsMobile =>
        _force == "mobile" ||
        (_force != "desktop" &&
         (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Tablet));
}
