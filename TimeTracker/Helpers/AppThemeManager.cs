namespace TimeTracker.Helpers;

public static class AppThemeManager
{
    private const string AppThemeKey = "AppThemePreference";

    public static readonly IReadOnlyList<string> ThemeOptions = new[] { "Auto", "Light", "Dark" };

    public static string GetSavedTheme()
    {
        var saved = Preferences.Get(AppThemeKey, "Auto");
        return ThemeOptions.Contains(saved) ? saved : "Auto";
    }

    public static void ApplySavedTheme(Application app) => ApplyTheme(app, GetSavedTheme());

    public static void SaveAndApplyTheme(Application app, string themeName)
    {
        var normalized = ThemeOptions.Contains(themeName) ? themeName : "Auto";
        Preferences.Set(AppThemeKey, normalized);
        ApplyTheme(app, normalized);
    }

    private static void ApplyTheme(Application app, string themeName)
    {
        app.UserAppTheme = themeName switch
        {
            "Light" => AppTheme.Light,
            "Dark" => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
