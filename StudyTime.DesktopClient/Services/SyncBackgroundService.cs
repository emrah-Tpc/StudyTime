using Microsoft.Extensions.Logging;
using StudyTime.DesktopClient.Offline;

namespace StudyTime.DesktopClient.Services
{
    /// <summary>
    /// Düzenli aralıklarla yerel Outbox verilerini API'ye eşitleyen arkaplan servisi.
    /// </summary>
    public class SyncBackgroundService
    {
        private readonly OutboxProcessor _outboxProcessor;
        private readonly ConnectivityService _connectivity;
        private readonly ILogger<SyncBackgroundService> _logger;
        private CancellationTokenSource? _cts;

        public SyncBackgroundService(
            OutboxProcessor outboxProcessor,
            ConnectivityService connectivity,
            ILogger<SyncBackgroundService> logger)
        {
            _outboxProcessor = outboxProcessor;
            _connectivity = connectivity;
            _logger = logger;
        }

        public void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            _ = ExecuteAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }

        private async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SyncBackgroundService started.");

            // Her 15 saniyede bir çalışacak şekilde ayarlandı
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (MauiProgram.IsOfflineBeta)
                    continue;

                if (_connectivity.IsOnline)
                {
                    _logger.LogInformation("SyncBackgroundService: Starting periodic flush.");
                    try
                    {
                        await _outboxProcessor.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "SyncBackgroundService: Error during flush.");
                    }
                }
                else
                {
                    _logger.LogDebug("SyncBackgroundService: Skipped flush due to being offline.");
                }
            }
        }
    }
}
