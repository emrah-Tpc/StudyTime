using Microsoft.Maui.Networking;
using StudyTime.DesktopClient;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// MAUI IConnectivity üzerinden ağ durumunu izler.
    /// Blazor bileşenleri ve servisler bu sınıfı dinler.
    /// </summary>
    public class ConnectivityService : IDisposable
    {
        private readonly IConnectivity _connectivity;
        private readonly StudyTimeAppOptions _appOptions;

        /// <summary>Ağ durumu değiştiğinde tetiklenir (true = online).</summary>
        public event Action<bool>? OnChanged;

        public ConnectivityService(IConnectivity connectivity, StudyTimeAppOptions appOptions)
        {
            _connectivity = connectivity;
            _appOptions   = appOptions;
            _connectivity.ConnectivityChanged += HandleChanged;
        }

        /// <summary>Şu an internet erişimi var mı? Yerel-only modda API denenmez (her zaman çevrimdışı sayılır).</summary>
        public bool IsOnline =>
            !MauiProgram.IsOfflineBeta &&
            !_appOptions.LocalOnlyMode &&
            (_connectivity.NetworkAccess == NetworkAccess.Internet ||
             _connectivity.NetworkAccess == NetworkAccess.ConstrainedInternet);

        private void HandleChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            bool nowOnline = e.NetworkAccess == NetworkAccess.Internet;
            OnChanged?.Invoke(nowOnline);
        }

        public void Dispose()
        {
            _connectivity.ConnectivityChanged -= HandleChanged;
        }
    }
}
