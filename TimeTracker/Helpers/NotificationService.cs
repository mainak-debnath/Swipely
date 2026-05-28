namespace TimeTracker.Helpers;

public static partial class NotificationService
{
    private const int GoalNotificationId = 101;
    private const int LongSessionNotificationId = 102;
    private const string GoalNotificationDateKey = "GoalNotificationDate";
    private const string LongSessionNotificationDateKey = "LongSessionNotificationDate";

    public static async Task EvaluateAsync(TrackingSnapshot snapshot)
    {
        if (NotificationPreferences.GoalNotificationsEnabled &&
            snapshot.TodayTotal >= snapshot.RequiredTime &&
            !WasSentToday(GoalNotificationDateKey))
        {
            bool sent = await ShowAsync(
                GoalNotificationId,
                "Daily goal reached",
                $"You have completed your {FormatDuration(snapshot.RequiredTime)} office goal for today.");

            if (sent)
            {
                MarkSentToday(GoalNotificationDateKey);
            }
        }

        var activeSession = snapshot.TodaySessions.FirstOrDefault(session => session.IsActiveToday);
        if (NotificationPreferences.LongSessionNotificationsEnabled &&
            activeSession is not null &&
            DateTime.Now - activeSession.InTime >= TimeSpan.FromHours(NotificationPreferences.LongSessionReminderHours) &&
            !WasSentToday(LongSessionNotificationDateKey))
        {
            bool sent = await ShowAsync(
                LongSessionNotificationId,
                "Still marked inside",
                $"You have been inside for {FormatDuration(DateTime.Now - activeSession.InTime)}. Swipe out if you have left.");

            if (sent)
            {
                MarkSentToday(LongSessionNotificationDateKey);
            }
        }
    }

    private static bool WasSentToday(string key) => Preferences.Get(key, string.Empty) == DateTime.Today.ToString("yyyy-MM-dd");

    private static void MarkSentToday(string key) => Preferences.Set(key, DateTime.Today.ToString("yyyy-MM-dd"));

    private static string FormatDuration(TimeSpan timeSpan) => $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes:D2}m";

    private static partial Task<bool> ShowAsync(int notificationId, string title, string message);
}
