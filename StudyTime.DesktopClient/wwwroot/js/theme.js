// Theme management - sets data-theme attribute on <html> and persists to localStorage
window.StudyTimeTheme = {
    setTheme: function (isDark) {
        var theme = isDark ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('studytime-theme', theme);
    },
    getTheme: function () {
        return localStorage.getItem('studytime-theme') || 'dark';
    }
};
