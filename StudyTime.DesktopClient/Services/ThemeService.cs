using System;

namespace StudyTime.DesktopClient.Services
{
    public class ThemeService
    {
        // Varsayılan tema: Dark
        public bool IsDarkMode { get; private set; } = true;

        public event Action? OnThemeChanged;

        public void SetTheme(bool isDark)
        {
            IsDarkMode = isDark;
            OnThemeChanged?.Invoke();
        }
    }
}