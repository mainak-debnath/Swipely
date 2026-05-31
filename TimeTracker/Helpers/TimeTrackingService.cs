using System.Text.Json;

namespace TimeTracker.Helpers;

public static class TimeTrackingService
{
    public const double DefaultOfficeHours = 8.0;
    public const int DefaultOfficeDaysPerWeek = 3;
    private const string OfficeHoursKey = "OfficeHoursGoal";
    private const string OfficeDaysPerWeekKey = "OfficeDaysPerWeekGoal";

    public static string StoragePath => Path.Combine(FileSystem.AppDataDirectory, "swipes.json");

    public static double GetOfficeHoursGoal() => Preferences.Get(OfficeHoursKey, DefaultOfficeHours);

    public static void SetOfficeHoursGoal(double hours) => Preferences.Set(OfficeHoursKey, hours);

    public static int GetOfficeDaysPerWeekGoal() => Preferences.Get(OfficeDaysPerWeekKey, DefaultOfficeDaysPerWeek);

    public static void SetOfficeDaysPerWeekGoal(int days) => Preferences.Set(OfficeDaysPerWeekKey, Math.Clamp(days, 1, 5));

    public static async Task<List<DateTime>> LoadSwipesAsync()
    {
        if (!File.Exists(StoragePath))
        {
            return new List<DateTime>();
        }

        var json = await File.ReadAllTextAsync(StoragePath);
        return JsonSerializer.Deserialize<List<DateTime>>(json) ?? new List<DateTime>();
    }

    public static List<DateTime> LoadSwipes()
    {
        if (!File.Exists(StoragePath))
        {
            return new List<DateTime>();
        }

        var json = File.ReadAllText(StoragePath);
        return JsonSerializer.Deserialize<List<DateTime>>(json) ?? new List<DateTime>();
    }

    public static async Task SaveSwipesAsync(IEnumerable<DateTime> swipes)
    {
        var ordered = swipes.OrderBy(swipe => swipe).ToList();
        var json = JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(StoragePath, json);
    }

    public static TrackingSnapshot BuildSnapshot(IReadOnlyList<DateTime> swipes, DateTime now)
    {
        var orderedSwipes = swipes.OrderBy(swipe => swipe).ToList();
        var requiredTime = TimeSpan.FromHours(GetOfficeHoursGoal());
        int requiredOfficeDaysPerWeek = GetOfficeDaysPerWeekGoal();
        var summaries = new Dictionary<DateTime, DaySummary>();
        var sessions = new List<SwipeSession>();

        for (int i = 0; i < orderedSwipes.Count; i += 2)
        {
            var inTime = orderedSwipes[i];
            DateTime? outTime = i + 1 < orderedSwipes.Count ? orderedSwipes[i + 1] : null;
            bool isActiveToday = !outTime.HasValue && inTime.Date == now.Date;

            if (!outTime.HasValue && inTime.Date < now.Date)
            {
                outTime = inTime.Date.AddDays(1).AddTicks(-1);
            }

            var session = new SwipeSession(inTime, outTime, isActiveToday);
            sessions.Add(session);

            var summaryDate = inTime.Date;
            if (!summaries.TryGetValue(summaryDate, out var summary))
            {
                summary = new DaySummary(summaryDate, new List<SwipeSession>());
                summaries[summaryDate] = summary;
            }

            summary.Sessions.Add(session);
        }

        foreach (var summary in summaries.Values)
        {
            summary.TotalTime = TimeSpan.Zero;
            summary.HasIncompleteSession = false;

            foreach (var session in summary.Sessions)
            {
                if (session.OutTime.HasValue)
                {
                    summary.TotalTime += session.OutTime.Value - session.InTime;
                }
                else
                {
                    summary.HasIncompleteSession = true;
                    if (session.InTime.Date == now.Date)
                    {
                        summary.TotalTime += now - session.InTime;
                    }
                }
            }
        }

        var today = now.Date;
        summaries.TryGetValue(today, out var todaySummary);
        var todaySessions = todaySummary?.Sessions.OrderByDescending(session => session.InTime).ToList() ?? new List<SwipeSession>();
        var todayTotal = todaySummary?.TotalTime ?? TimeSpan.Zero;

        var latestSwipe = orderedSwipes.LastOrDefault();
        bool hasAnySwipe = orderedSwipes.Count > 0;
        bool latestSwipeWasIn = hasAnySwipe && orderedSwipes.Count % 2 == 1;
        bool isCurrentlyInside = hasAnySwipe && latestSwipeWasIn && latestSwipe.Date == today;
        var timeLeft = requiredTime - todayTotal;
        if (timeLeft < TimeSpan.Zero)
        {
            timeLeft = TimeSpan.Zero;
        }

        int activeDaysThisMonth = summaries.Values.Count(summary =>
            summary.Date.Year == now.Year &&
            summary.Date.Month == now.Month &&
            summary.TotalTime > TimeSpan.Zero);

        var weekStart = StartOfWeek(today);
        var weekEnd = weekStart.AddDays(6);
        int activeDaysThisWeek = summaries.Values.Count(summary =>
            summary.Date >= weekStart &&
            summary.Date <= weekEnd &&
            summary.TotalTime > TimeSpan.Zero);

        int officeDaysLeftThisWeek = Math.Max(requiredOfficeDaysPerWeek - activeDaysThisWeek, 0);
        var weeklyStreak = CalculateWeeklyStreak(summaries.Values, weekStart, requiredOfficeDaysPerWeek);

        int goalDaysThisMonth = summaries.Values.Count(summary =>
            summary.Date.Year == now.Year &&
            summary.Date.Month == now.Month &&
            summary.TotalTime >= requiredTime);

        int currentStreak = 0;
        for (var cursor = today; summaries.TryGetValue(cursor, out var summary) && summary.TotalTime >= requiredTime; cursor = cursor.AddDays(-1))
        {
            currentStreak++;
        }

        return new TrackingSnapshot(
            requiredTime,
            todayTotal,
            timeLeft,
            isCurrentlyInside,
            latestSwipe == default ? null : latestSwipe,
            latestSwipeWasIn,
            todaySessions,
            summaries,
            activeDaysThisMonth,
            requiredOfficeDaysPerWeek,
            activeDaysThisWeek,
            officeDaysLeftThisWeek,
            weeklyStreak.Current,
            weeklyStreak.Best,
            goalDaysThisMonth,
            currentStreak);
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        int daysSinceMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.Date.AddDays(-daysSinceMonday);
    }

    private static (int Current, int Best) CalculateWeeklyStreak(IEnumerable<DaySummary> summaries, DateTime currentWeekStart, int requiredOfficeDaysPerWeek)
    {
        var completedWeeks = summaries
            .Where(summary => summary.TotalTime > TimeSpan.Zero)
            .GroupBy(summary => StartOfWeek(summary.Date))
            .Where(group => group.Count() >= requiredOfficeDaysPerWeek)
            .Select(group => group.Key)
            .OrderBy(week => week)
            .ToList();

        var completedWeekSet = completedWeeks.ToHashSet();
        var streakCursor = completedWeekSet.Contains(currentWeekStart)
            ? currentWeekStart
            : currentWeekStart.AddDays(-7);

        int current = 0;
        while (completedWeekSet.Contains(streakCursor))
        {
            current++;
            streakCursor = streakCursor.AddDays(-7);
        }

        int best = 0;
        int running = 0;
        DateTime? previousWeek = null;
        foreach (var week in completedWeeks)
        {
            running = previousWeek.HasValue && week == previousWeek.Value.AddDays(7)
                ? running + 1
                : 1;
            best = Math.Max(best, running);
            previousWeek = week;
        }

        return (current, best);
    }
}

public sealed record TrackingSnapshot(
    TimeSpan RequiredTime,
    TimeSpan TodayTotal,
    TimeSpan TimeLeft,
    bool IsCurrentlyInside,
    DateTime? LastSwipeTime,
    bool LastSwipeWasIn,
    IReadOnlyList<SwipeSession> TodaySessions,
    IReadOnlyDictionary<DateTime, DaySummary> Summaries,
    int ActiveDaysThisMonth,
    int RequiredOfficeDaysPerWeek,
    int ActiveDaysThisWeek,
    int OfficeDaysLeftThisWeek,
    int WeeklyStreak,
    int BestWeeklyStreak,
    int GoalDaysThisMonth,
    int CurrentStreak);

public sealed record SwipeSession(DateTime InTime, DateTime? OutTime, bool IsActiveToday);

public sealed class DaySummary
{
    public DaySummary(DateTime date, List<SwipeSession> sessions)
    {
        Date = date;
        Sessions = sessions;
    }

    public DateTime Date { get; }
    public List<SwipeSession> Sessions { get; }
    public TimeSpan TotalTime { get; set; }
    public bool HasIncompleteSession { get; set; }
}
