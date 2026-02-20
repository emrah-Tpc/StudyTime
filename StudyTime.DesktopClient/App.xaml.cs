namespace StudyTime.DesktopClient
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new MainPage())
            {
                Title = "StudyTime",
                MinimumWidth = 900,
                MinimumHeight = 620
            };
            return window;
        }
    }
}
