namespace TimeTracker.Helpers;

public static class NotificationPreferences
{
    private const string GoalNotificationsKey = "GoalNotificationsEnabled";
    private const string LongSessionNotificationsKey = "LongSessionNotificationsEnabled";
    private const string LongSessionHoursKey = "LongSessionReminderHours";

    public static bool GoalNotificationsEnabled
    {
        get => Preferences.Get(GoalNotificationsKey, true);
        set => Preferences.Set(GoalNotificationsKey, value);
    }

    public static bool LongSessionNotificationsEnabled
    {
        get => Preferences.Get(LongSessionNotificationsKey, true);
        set => Preferences.Set(LongSessionNotificationsKey, value);
    }

    public static double LongSessionReminderHours
    {
        get => Preferences.Get(LongSessionHoursKey, 10.0);
        set => Preferences.Set(LongSessionHoursKey, Math.Max(1.0, value));
    }
}
