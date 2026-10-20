namespace ITF_Taekwondo_Gradering
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            AppThemeManager.ApplySavedTheme(this);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            WebConnection.ChagiDataSyncService.Initialize();
            var window = new Window(new AppShell());
            window.Activated += (_, _) => WebConnection.ChagiDataSyncService.SetForeground(true);
            window.Resumed += (_, _) => WebConnection.ChagiDataSyncService.SetForeground(true);
            window.Deactivated += (_, _) => WebConnection.ChagiDataSyncService.SetForeground(false);
            window.Stopped += (_, _) => WebConnection.ChagiDataSyncService.SetForeground(false);
            window.Destroying += (_, _) => WebConnection.ChagiDataSyncService.SetForeground(false);

#if WINDOWS
    window.Width = 570;
    window.Height = 900;
#endif

            return window;
        }
    }
}
