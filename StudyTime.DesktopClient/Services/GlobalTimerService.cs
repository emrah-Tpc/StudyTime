using System;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace StudyTime.DesktopClient.Services
{
    public class GlobalTimerService
    {
        private readonly StudySessionApiService _apiService;
        private Timer? _timer;

        // ── Temel Durum ──────────────────────────────────────────────────────
        public bool IsRunning      { get; private set; }
        public bool IsPaused       { get; private set; }
        public bool IsFocusModeActive { get; private set; }
        public Guid? ActiveLessonId   { get; private set; }
        public Guid? ActiveTaskId     { get; private set; }
        public Guid CurrentSessionId  { get; private set; }
        public string? ActiveTaskTitle { get; private set; }

        // Çalışma sırasındaki orijinal renk; mola sırasında yeşile döner
        private string _workColor = "#ff4b4b";
        public string ActiveColor { get; set; } = "#ff4b4b";

        // ── Zamanlayıcı Modu ─────────────────────────────────────────────────
        public bool IsCountdown        { get; private set; }
        public bool IsBreak            { get; private set; }
        public TimeSpan InitialDuration { get; private set; }
        public TimeSpan BreakDuration   { get; private set; }
        public TimeSpan ElapsedTime     { get; private set; }

        /// <summary>Kalan süre: geri sayımda azalır, kronom-etre modunda ElapsedTime ile aynıdır.</summary>
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

        public GlobalTimerService(StudySessionApiService apiService)
        {
            _apiService = apiService;
        }

        // ── Çalışma Oturumu Başlat ───────────────────────────────────────────
        /// <param name="countdown">null → kronometre; değer → geri sayım</param>
        /// <param name="breakDuration">Çalışma bitince otomatik başlayacak mola süresi</param>
        public async Task StartAsync(
            Guid lessonId,
            Guid? taskId,
            string color          = "#ff4b4b",
            string? taskTitle     = null,
            TimeSpan? countdown   = null,
            TimeSpan? breakDuration = null)
        {
            if (IsRunning && !IsPaused) return;

            if (IsPaused && ActiveLessonId == lessonId && !IsBreak)
            {
                Resume();
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
                ElapsedTime     = TimeSpan.Zero;

                IsRunning = true;
                IsPaused  = false;

                StartLocalTimer();
            }
        }

        // ── Mola Başlat (UI tarafından çağrılır) ────────────────────────────
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
            ElapsedTime     = TimeSpan.Zero;
            ActiveColor     = "#22c55e";  // Dinlendirici yeşil

            IsRunning = true;
            IsPaused  = false;

            StartLocalTimer();
        }

        // ── Kontroller ───────────────────────────────────────────────────────
        public void Pause()
        {
            _timer?.Stop();
            IsRunning = false;
            IsPaused  = true;
            OnTick?.Invoke();
        }

        public void Resume()
        {
            StartLocalTimer();
            IsRunning = true;
            IsPaused  = false;
        }

        public async Task StopAsync()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            IsRunning         = false;
            IsPaused          = false;
            IsFocusModeActive = false;
            IsCountdown       = false;
            IsBreak           = false;
            InitialDuration   = TimeSpan.Zero;
            BreakDuration     = TimeSpan.Zero;

            var sessionId = CurrentSessionId;
            ActiveLessonId  = null;
            ActiveTaskId    = null;
            ActiveTaskTitle = null;
            ElapsedTime     = TimeSpan.Zero;
            ActiveColor     = _workColor;

            if (sessionId != Guid.Empty)
            {
                await _apiService.StopSessionAsync(sessionId);
                CurrentSessionId = Guid.Empty;
            }

            OnFocusModeChanged?.Invoke();
            OnTick?.Invoke();
        }

        public void SetFocusMode(bool active)
        {
            IsFocusModeActive = active;
            OnFocusModeChanged?.Invoke();
        }

        // ── İç Zamanlayıcı ───────────────────────────────────────────────────
        private void StartLocalTimer()
        {
            _timer?.Dispose();
            _timer = new Timer(1000);
            _timer.Elapsed += async (s, e) =>
            {
                ElapsedTime = ElapsedTime.Add(TimeSpan.FromSeconds(1));

                if (IsCountdown && ElapsedTime >= InitialDuration)
                {
                    ElapsedTime = InitialDuration;
                    _timer?.Stop();
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
                        // Çalışma bitti → oturumu kapat ve OnTimerFinished tetikle
                        // Mola başlatmak Workspace.razor'un sorumluluğuna bırakıldı
                        await _apiService.StopSessionAsync(CurrentSessionId);
                        CurrentSessionId = Guid.Empty;
                        OnTimerFinished?.Invoke();
                    }
                    return;
                }

                OnTick?.Invoke();
            };
            _timer.Start();
        }
    }
}