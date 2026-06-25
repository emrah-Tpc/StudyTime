using Microsoft.Maui.Devices;

namespace StudyTime.DesktopClient.Services;

public class PlatformDetector : IPlatformDetector
{
    public bool IsDesktop => DeviceInfo.Current.Idiom == DeviceIdiom.Desktop;
    
    public bool IsMobile => DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Tablet;
}
