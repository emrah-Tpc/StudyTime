namespace StudyTime.DesktopClient.Services
{
    /// <summary>
    /// Tracks the current theme state in memory and notifies subscribers.
    /// The actual DOM update (data-theme attribute) is done by components
    /// via JS interop (StudyTimeTheme.setTheme) after calling SetTheme().
    /// </summary>
    public class ThemeService
    {
        public bool IsDarkMode { get; private set; } = true;

        public event Action? OnThemeChanged;

        public void SetTheme(bool isDark)
        {
            IsDarkMode = isDark;
            OnThemeChanged?.Invoke();
        }
    }
}