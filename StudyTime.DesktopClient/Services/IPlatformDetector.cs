namespace StudyTime.DesktopClient.Services;

public interface IPlatformDetector
{
    bool IsDesktop { get; }
    bool IsMobile { get; }
}
