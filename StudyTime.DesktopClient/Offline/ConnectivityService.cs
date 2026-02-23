using Microsoft.Maui.Networking;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// MAUI IConnectivity üzerinden ağ durumunu izler.
    /// Blazor bileşenleri ve servisler bu sınıfı dinler.
    /// </summary>
    public class ConnectivityService : IDisposable
    {
        private readonly IConnectivity _connectivity;

        /// <summary>Ağ durumu değiştiğinde tetiklenir (true = online).</summary>
        public event Action<bool>? OnChanged;

        public ConnectivityService(IConnectivity connectivity)
        {
            _connectivity = connectivity;
            _connectivity.ConnectivityChanged += HandleChanged;
        }

        /// <summary>Şu an internet erişimi var mı?</summary>
        public bool IsOnline =>
            _connectivity.NetworkAccess == NetworkAccess.Internet ||
            _connectivity.NetworkAccess == NetworkAccess.ConstrainedInternet;

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
