using TimeTracker.Helpers;

namespace TimeTracker;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        AppThemeManager.ApplySavedTheme(this);
        MainPage = new AppShell();
    }
}
