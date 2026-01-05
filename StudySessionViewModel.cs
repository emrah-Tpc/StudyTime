using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StudyTime.Domain.Entities; // Senin Entity'nin olduğu yer

namespace StudyTime.ViewModels // Namespace ismine dikkat et
{
    public class StudySessionViewModel : INotifyPropertyChanged
    {
        private IDispatcherTimer _timer;
        private StudySession _currentSession;

        // UI'ın dinlediği Saat metni (00:00:00)
        private string _timerText = "00:00:00";
        public string TimerText
        {
            get => _timerText;
            set
            {
                _timerText = value;
                OnPropertyChanged(); // UI'a haber ver: "Değiştim!"
            }
        }

        // Buton Komutları
        public ICommand StartCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand StopCommand { get; }

        public StudySessionViewModel()
        {
            // 1. Session oluştur (Örnek bir ID ile)
            _currentSession = new StudySession(Guid.NewGuid());

            // 2. Timer'ı kur (Her 1 saniyede bir çalışacak)
            _timer = Application.Current.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => UpdateTimerDisplay();

            // 3. Butonları tanımla
            StartCommand = new Command(() =>
            {
                _currentSession.Start();
                _timer.Start(); // Sayacı başlat
            });

            PauseCommand = new Command(() =>
            {
                _currentSession.Pause();
                _timer.Stop(); // Sayacı durdur
                UpdateTimerDisplay(); // Son hali ekrana yaz
            });

            StopCommand = new Command(() =>
            {
                _currentSession.Stop();
                _timer.Stop();
                UpdateTimerDisplay();
            });
        }

        // Her saniye çalışan metod
        private void UpdateTimerDisplay()
        {
            // Domain'e eklediğimiz CurrentDuration property'sini burada kullanıyoruz
            // NOT: StudySession classına CurrentDuration eklediğinden emin ol!
            TimerText = _currentSession.CurrentDuration.ToString(@"hh\:mm\:ss");
        }

        // -- Burası Standart MVVM Altyapısıdır (Ezberlemene gerek yok, kopyala yeter) --
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}