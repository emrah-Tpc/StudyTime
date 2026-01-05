using System;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace StudyTime.DesktopClient.Services
{
    public class GlobalTimerService
    {
        private readonly StudySessionApiService _apiService;
        private Timer? _timer;

        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public Guid? ActiveLessonId { get; private set; }
        public Guid? ActiveTaskId { get; private set; }
        public Guid CurrentSessionId { get; private set; }
        public TimeSpan ElapsedTime { get; private set; }
        public string? ActiveTaskTitle { get; private set; }

        // Varsayılan renk kırmızı
        public string ActiveColor { get; set; } = "#ff4b4b";

        public event Action? OnTick;

        public GlobalTimerService(StudySessionApiService apiService)
        {
            _apiService = apiService;
        }

        // 👇 DEĞİŞİKLİK BURADA: Artık 'color' parametresi de alıyor
        public async Task StartAsync(Guid lessonId, Guid? taskId, string color = "#ff4b4b")
        {
            if (IsRunning && !IsPaused) return;

            if (IsPaused && ActiveLessonId == lessonId)
            {
                Resume();
                return;
            }

            // Yeni Oturum Başlat
            CurrentSessionId = await _apiService.StartSessionAsync(lessonId, taskId);

            if (CurrentSessionId != Guid.Empty)
            {
                ActiveLessonId = lessonId;
                ActiveTaskId = taskId;

                // 👇 RENGİ BURADA GÜNCELLİYORUZ
                ActiveColor = color;

                ElapsedTime = TimeSpan.Zero;
                IsRunning = true;
                IsPaused = false;

                StartLocalTimer();
            }
        }

        public void Pause()
        {
            _timer?.Stop();
            IsRunning = false;
            IsPaused = true;
            OnTick?.Invoke();
        }

        public void Resume()
        {
            StartLocalTimer();
            IsRunning = true;
            IsPaused = false;
        }

        public async Task StopAsync()
        {
            _timer?.Stop();
            _timer?.Dispose();
            IsRunning = false;
            IsPaused = false;
            ActiveLessonId = null;
            ActiveTaskId = null;

            // İstersen durdurunca rengi sıfırlayabilirsin
            // ActiveColor = "#ff4b4b"; 

            if (CurrentSessionId != Guid.Empty)
            {
                await _apiService.StopSessionAsync(CurrentSessionId);
                CurrentSessionId = Guid.Empty;
            }
        }

        private void StartLocalTimer()
        {
            _timer?.Dispose();
            _timer = new Timer(1000);
            _timer.Elapsed += (s, e) =>
            {
                ElapsedTime = ElapsedTime.Add(TimeSpan.FromSeconds(1));
                OnTick?.Invoke();
            };
            _timer.Start();
        }
    }
}