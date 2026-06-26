using System;
using System.Threading.Tasks;
using StudyTime.Application;
using StudyTime.DesktopClient.Offline;
using Timer = System.Timers.Timer;

namespace StudyTime.DesktopClient.Services
{
    public class GlobalTimerService
    {
        private readonly SyncedStudySessionApiService _apiService;
        private Timer? _timer;

        // ── Timestamp-bazlı hesaplama (Background güvenli) ───────────────────
        // Timer sadece UI'yı tickler; gerçek süre duvar saatinden hesaplanır
        private DateTime? _sessionStartedAt;
        private TimeSpan _accumulatedBeforePause = TimeSpan.Zero;

        // ── Temel Durum ──────────────────────────────────────────────────────
        public bool IsRunning         { get; private set; }
        public bool IsPaused          { get; private set; }
        public bool IsFocusModeActive { get; private set; }
        public Guid? ActiveLessonId   { get; private set; }
        public Guid? ActiveTaskId     { get; private set; }
        public Guid CurrentSessionId  { get; private set; }
        public string? ActiveTaskTitle { get; private set; }
        public string? ActiveModeName  { get; private set; }

        // Çalışma sırasındaki orijinal renk; mola sırasında yeşile döner
        private string _workColor = "#ff4b4b";
        public string ActiveColor { get; set; } = "#ff4b4b";

        // ── Zamanlayıcı Modu ─────────────────────────────────────────────────
        public bool IsCountdown         { get; private set; }
        public bool IsBreak             { get; private set; }
        public TimeSpan InitialDuration { get; private set; }
        public TimeSpan BreakDuration   { get; private set; }

        /// <summary>
        /// Geçen süre — timestamp bazlı, background'da kaybedilmez.
        /// </summary>
        public TimeSpan ElapsedTime
        {
            get
            {
                if (_sessionStartedAt == null)
                    return _accumulatedBeforePause;
                return _accumulatedBeforePause + (DateTime.Now - _sessionStartedAt.Value);
            }
        }

        /// <summary>Kalan süre: geri sayımda azalır, kronometrede ElapsedTime ile aynıdır.</summary>
        public TimeSpan RemainingTime =>
            IsCountdown
                ? (InitialDuration > ElapsedTime ? InitialDuration - ElapsedTime : TimeSpan.Zero)
                : ElapsedTime;

        // ── Events ───────────────────────────────────────────────────────────
        public event Action? OnTick;
        public event Action? OnFocusModeChanged;
        /// <summary>Çalışma süresi 00:00'a ulaştığında tetiklenir (mola başlamadan önce).</summary>
        public event Action? OnTimerFinished;
        /// <summary>Mola süresi 00:00'a ulaştığında tetiklenir.</summary>
        public event Action? OnBreakFinished;
        /// <summary>Kullanıcı kronometreyi manuel durdurduğunda tetiklenir.</summary>
        public event Action? OnTimerStopped;

        // ── Dashboard bugünkü süre (singleton — saniye hassasiyeti) ─────────────
        private int _displayHighWaterMarkSeconds;
        private int _displayStoppedBridgeSeconds;
        private int _summaryBaselineSeconds;
        private int _sessionsSecondsAddedLocally;
        private int _displayStateDayOfYear = -1;

        public int DisplayTodayStudiedTotalSeconds
        {
            get
            {
                EnsureDisplayStateDay();
                var current = ComputeCurrentDisplaySeconds();
                if (current > _displayHighWaterMarkSeconds)
                    _displayHighWaterMarkSeconds = current;
                return _displayHighWaterMarkSeconds;
            }
        }

        public int DisplayTodayStudiedMinutes =>
            StudyDurationMetrics.ToChartMinutesFromTotalSeconds(DisplayTodayStudiedTotalSeconds);

        public string DisplayTodayStudiedLabel =>
            StudyDurationMetrics.FormatDisplay(TimeSpan.FromSeconds(DisplayTodayStudiedTotalSeconds));

        public void SetSummaryBaseline(int todayStudiedMinutes)
        {
            EnsureDisplayStateDay();
            _summaryBaselineSeconds = todayStudiedMinutes * 60;
            var current = ComputeCurrentDisplaySeconds();
            if (current > _displayHighWaterMarkSeconds)
                _displayHighWaterMarkSeconds = current;
        }

        public void MergeApiSummaryMinutes(int apiTodayMinutes)
        {
            EnsureDisplayStateDay();
            _summaryBaselineSeconds = apiTodayMinutes * 60;

            if (IsRunning && !IsBreak)
            {
                var live = ComputeCurrentDisplaySeconds();
                if (live > _displayHighWaterMarkSeconds)
                    _displayHighWaterMarkSeconds = live;
                return;
            }

            var apiSeconds = apiTodayMinutes * 60;
            if (apiSeconds >= _displayHighWaterMarkSeconds)
            {
                _displayHighWaterMarkSeconds = apiSeconds;
                _displayStoppedBridgeSeconds = 0;
                _sessionsSecondsAddedLocally = 0;
            }
        }

        public void ClearDashboardDisplayState()
        {
            _displayHighWaterMarkSeconds = 0;
            _displayStoppedBridgeSeconds = 0;
            _summaryBaselineSeconds = 0;
            _sessionsSecondsAddedLocally = 0;
            _displayStateDayOfYear = -1;
        }

        private int ActiveSessionExtraSeconds =>
            IsRunning && !IsBreak ? (int)ElapsedTime.TotalSeconds : 0;

        private int ComputeCurrentDisplaySeconds()
        {
            var baseTotal = _summaryBaselineSeconds + _sessionsSecondsAddedLocally;
            if (IsRunning && !IsBreak)
                return baseTotal + ActiveSessionExtraSeconds;

            return Math.Max(baseTotal, _displayStoppedBridgeSeconds);
        }

        private void CaptureHighWaterOnSessionEnd()
        {
            if (IsBreak) return;

            EnsureDisplayStateDay();
            var sessionSeconds = (int)ElapsedTime.TotalSeconds;
            if (sessionSeconds < 1) return;

            _sessionsSecondsAddedLocally += sessionSeconds;
            var total = _summaryBaselineSeconds + _sessionsSecondsAddedLocally;
            if (total > _displayHighWaterMarkSeconds)
                _displayHighWaterMarkSeconds = total;
            _displayStoppedBridgeSeconds = _displayHighWaterMarkSeconds;
        }

        private void EnsureDisplayStateDay()
        {
            var today = DateTime.Today.DayOfYear;
            if (_displayStateDayOfYear == today) return;

            _displayHighWaterMarkSeconds = 0;
            _displayStoppedBridgeSeconds = 0;
            _summaryBaselineSeconds = 0;
            _sessionsSecondsAddedLocally = 0;
            _displayStateDayOfYear = today;
        }

        public GlobalTimerService(SyncedStudySessionApiService apiService)
        {
            _apiService = apiService;
        }

        // ── Çalışma Oturumu Başlat ───────────────────────────────────────────
        /// <param name="countdown">null → kronometre; değer → geri sayım</param>
        /// <param name="breakDuration">Çalışma bitince otomatik başlayacak mola süresi</param>
        public async Task StartAsync(
            Guid lessonId,
            Guid? taskId,
            string color            = "#ff4b4b",
            string? taskTitle       = null,
            TimeSpan? countdown     = null,
            TimeSpan? breakDuration = null)
        {
            if (IsRunning && !IsPaused) return;

            if (IsPaused && ActiveLessonId == lessonId && !IsBreak)
            {
                await ResumeAsync();
                return;
            }

            CurrentSessionId = await _apiService.StartSessionAsync(lessonId, taskId, isBreak: false);

            if (CurrentSessionId != Guid.Empty)
            {
                ActiveLessonId  = lessonId;
                ActiveTaskId    = taskId;
                ActiveTaskTitle = taskTitle;
                _workColor      = color;
                ActiveColor     = color;

                IsCountdown     = countdown.HasValue;
                InitialDuration = countdown ?? TimeSpan.Zero;
                BreakDuration   = breakDuration ?? TimeSpan.Zero;
                IsBreak         = false;

                // Mod adını belirle
                ActiveModeName = countdown.HasValue
                    ? (countdown.Value.TotalMinutes <= 25 ? "Klasik"
                        : countdown.Value.TotalMinutes <= 50 ? "Derin Odak"
                        : "Özel")
                    : "Serbest";

                // Timestamp sıfırla
                _accumulatedBeforePause = TimeSpan.Zero;
                _sessionStartedAt       = null;

                IsRunning = true;
                IsPaused  = false;

                StartLocalTimer();
            }
        }

        // ── Mola Başlat (UI tarafından veya otomatik çağrılır) ───────────────
        public async Task StartBreakAsync()
        {
            if (!ActiveLessonId.HasValue || BreakDuration == TimeSpan.Zero) return;

            // Mola oturumunu kaydet (IsBreak = true)
            var breakSessionId = await _apiService.StartSessionAsync(
                ActiveLessonId.Value, ActiveTaskId, isBreak: true);

            if (breakSessionId != Guid.Empty)
                CurrentSessionId = breakSessionId;

            IsBreak         = true;
            IsCountdown     = true;
            InitialDuration = BreakDuration;
            ActiveColor     = "#22c55e";  // Dinlendirici yeşil

            // Timestamp sıfırla
            _accumulatedBeforePause = TimeSpan.Zero;
            _sessionStartedAt       = null;

            IsRunning = true;
            IsPaused  = false;

            StartLocalTimer();
        }

        // ── Kontroller ───────────────────────────────────────────────────────
        public async Task PauseAsync()
        {
            _timer?.Stop();
            // Birikmiş süreyi kaydet; sessionStartedAt'ı temizle
            _accumulatedBeforePause = ElapsedTime;
            _sessionStartedAt       = null;

            IsRunning = false;
            IsPaused  = true;
            OnTick?.Invoke();

            if (CurrentSessionId != Guid.Empty)
            {
                await _apiService.PauseSessionAsync(CurrentSessionId);
            }
        }

        public async Task ResumeAsync()
        {
            StartLocalTimer();
            IsRunning = true;
            IsPaused  = false;

            if (CurrentSessionId != Guid.Empty)
            {
                await _apiService.ResumeSessionAsync(CurrentSessionId);
            }
        }

        public async Task StopAsync()
        {
            var wasActiveTimer = IsRunning || IsPaused;

            if (wasActiveTimer && !IsBreak)
                CaptureHighWaterOnSessionEnd();

            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            // Timestamp sıfırla
            _accumulatedBeforePause = TimeSpan.Zero;
            _sessionStartedAt       = null;

            IsRunning         = false;
            IsPaused          = false;
            IsFocusModeActive = false;
            IsCountdown       = false;
            IsBreak           = false;
            InitialDuration   = TimeSpan.Zero;
            BreakDuration     = TimeSpan.Zero;

            var sessionId   = CurrentSessionId;
            ActiveLessonId  = null;
            ActiveTaskId    = null;
            ActiveTaskTitle = null;
            ActiveModeName  = null;
            ActiveColor     = _workColor;

            if (sessionId != Guid.Empty)
            {
                await _apiService.StopSessionAsync(sessionId);
                CurrentSessionId = Guid.Empty;
            }

            OnFocusModeChanged?.Invoke();
            OnTick?.Invoke();
            if (wasActiveTimer)
                OnTimerStopped?.Invoke();
        }

        public void SetFocusMode(bool active)
        {
            IsFocusModeActive = active;
            OnFocusModeChanged?.Invoke();
        }

        public async Task ForceResetAsync()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            _accumulatedBeforePause = TimeSpan.Zero;
            _sessionStartedAt       = null;

            IsRunning         = false;
            IsPaused          = false;
            IsFocusModeActive = false;
            IsCountdown       = false;
            IsBreak           = false;
            InitialDuration   = TimeSpan.Zero;
            BreakDuration     = TimeSpan.Zero;

            if (CurrentSessionId != Guid.Empty)
            {
                await _apiService.StopSessionAsync(CurrentSessionId);
            }

            CurrentSessionId  = Guid.Empty;
            ActiveLessonId  = null;
            ActiveTaskId    = null;
            ActiveTaskTitle = null;
            ActiveModeName  = null;
            ActiveColor     = _workColor;

            ClearDashboardDisplayState();
            OnFocusModeChanged?.Invoke();
            OnTick?.Invoke();
        }

        /// <summary>
        /// Uygulama foreground'a döndüğünde çağrılır.
        /// Timestamp-bazlı hesaplama sayesinde süre zaten doğrudur;
        /// bu metot yalnızca UI'yı yeniler.
        /// </summary>
        public void NotifyForeground()
        {
            OnTick?.Invoke();
        }

        // ── İç Zamanlayıcı ───────────────────────────────────────────────────
        private void StartLocalTimer()
        {
            _timer?.Dispose();

            // Timestamp: timer başladığında başlangıç noktasını kaydet
            _sessionStartedAt = DateTime.Now - _accumulatedBeforePause;
            // Birikmiş süreyi sıfırla — artık fark hesaplanacak
            _accumulatedBeforePause = TimeSpan.Zero;

            _timer = new Timer(1000);
            _timer.Elapsed += async (s, e) =>
            {
              try
              {
                // Süre kontrolü: ElapsedTime property'si timestamp'ten hesaplar
                if (IsCountdown && ElapsedTime >= InitialDuration)
                {
                    _timer?.Stop();
                    // Birikmiş süreyi sabitle
                    _accumulatedBeforePause = InitialDuration;
                    _sessionStartedAt       = null;

                    IsRunning = false;
                    IsPaused  = false;
                    OnTick?.Invoke();

                    if (IsBreak)
                    {
                        // Mola bitti → durum sıfırla, event tetikle
                        IsBreak     = false;
                        ActiveColor = _workColor;
                        await _apiService.StopSessionAsync(CurrentSessionId);
                        CurrentSessionId = Guid.Empty;
                        OnBreakFinished?.Invoke();
                    }
                    else
                    {
                        // Çalışma bitti → oturumu kapat
                        CaptureHighWaterOnSessionEnd();
                        await _apiService.StopSessionAsync(CurrentSessionId);
                        CurrentSessionId = Guid.Empty;
                        // NOT: ActiveLessonId ve BreakDuration burada HÂLÂ DOLU
                        // Workspace.razor'daki OnTimerFinished handler bunları okuyabilir
                        OnTimerFinished?.Invoke();
                    }
                    return;
                }

                // Normal tick — sadece UI'yı yenile
                OnTick?.Invoke();
              }
              catch (Exception ex)
              {
                // F14: async void timer handler'da gözlemlenmeyen exception app'i çökertmesin.
                System.Diagnostics.Debug.WriteLine($"Timer Elapsed handler failed: {ex.Message}");
              }
            };
            _timer.Start();
        }
    }
}