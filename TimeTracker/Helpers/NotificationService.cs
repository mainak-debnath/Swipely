namespace TimeTracker.Helpers;

public static partial class NotificationService
{
    private const int GoalNotificationId = 101;
    private const int LongSessionNotificationId = 102;
    private const string GoalNotificationDateKey = "GoalNotificationDate";
    private const string LongSessionNotificationDateKey = "LongSessionNotificationDate";

    private static readonly string[] GoalTitles = new[]
    {
        "Goal Smashed!",
        "Escape Room Cleared!",
        "Quest Complete!",
        "Achievement Unlocked!",
        "Freedom!"
    };

    private static readonly string[] LongSessionTitles = new[]
    {
        "Are you okay?",
        "Working hard or hardly working?",
        "Overtime Alert!",
        "Ghost in the Office?",
        "Time Check!"
    };

    public static async Task EvaluateAsync(TrackingSnapshot snapshot)
    {
        var random = new Random();
        if (NotificationPreferences.GoalNotificationsEnabled &&
            snapshot.TodayTotal >= snapshot.RequiredTime &&
            !WasSentToday(GoalNotificationDateKey))
        {
            string goalTime = FormatDuration(snapshot.RequiredTime);

            string[] goalMessages = new[]
            {
            $"Boom! You just crushed your {goalTime} office goal. Class dismissed!",
            $"You survived your {goalTime} grind today. Close those tabs and run!",
            $"You logged {goalTime} of solid adulting today. +100 productivity points!",
            $"That's {goalTime} of hard work in the books. Go touch some grass and eat some fried chicken!",
            $"Successfully clocked your {goalTime}. The coffee machine will miss you."
        };

            string title = GoalTitles[random.Next(GoalTitles.Length)];
            string message = goalMessages[random.Next(goalMessages.Length)];

            bool sent = await ShowAsync(GoalNotificationId, title, message);
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
            string sessionDuration = FormatDuration(DateTime.Now - activeSession.InTime);

            string[] longSessionMessages = new[]
            {
            $"You've been clocked in for {sessionDuration}. Did you lock yourself in the bathroom? Swipe out if you left!",
            $"You've been inside for {sessionDuration}. They're going to start charging you rent. Swipe out if you're actually home!",
            $"Warning: You've been here for {sessionDuration}. Remember what your family looks like? Swipe out if you left.",
            $"Current session: {sessionDuration}. Unless you are plotting to overthrow management, swipe out if you've departed!",
            $"You've logged {sessionDuration} straight. If you aren't physically there, don't forget to swipe out!"
        };

            string title = LongSessionTitles[random.Next(LongSessionTitles.Length)];
            string message = longSessionMessages[random.Next(longSessionMessages.Length)];

            bool sent = await ShowAsync(LongSessionNotificationId, title, message);
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
