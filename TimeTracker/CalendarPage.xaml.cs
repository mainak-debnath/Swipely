using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls.Shapes;
using TimeTracker.Helpers;

namespace TimeTracker;

public partial class CalendarPage : ContentPage
{
    private TrackingSnapshot? snapshot;
    private DateTime currentDate = DateTime.Today;
    private DateTime? selectedDate;

    public CalendarPage()
    {
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<OfficeHoursUpdatedMessage>(this, (_, _) => Refresh());
        WeakReferenceMessenger.Default.Register<LogsClearedMessage>(this, (_, _) => Refresh());
        WeakReferenceMessenger.Default.Register<AllLogsClearedMessage>(this, (_, _) => Refresh());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        snapshot = TimeTrackingService.BuildSnapshot(TimeTrackingService.LoadSwipes(), DateTime.Now);
        BuildCalendar(currentDate.Year, currentDate.Month);
    }

    private void BuildCalendar(int year, int month)
    {
        MonthYearLabel.Text = new DateTime(year, month, 1).ToString("MMMM yyyy");

        CalendarGrid.RowDefinitions.Clear();
        CalendarGrid.ColumnDefinitions.Clear();
        CalendarGrid.Children.Clear();

        for (int i = 0; i < 7; i++)
        {
            CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (int i = 0; i < 7; i++)
        {
            CalendarGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        string[] weekDays = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        for (int i = 0; i < weekDays.Length; i++)
        {
            var label = new Label
            {
                Text = weekDays[i],
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = ThemeColor("TextSecondaryLight", "TextSecondaryDark"),
                HorizontalTextAlignment = TextAlignment.Center
            };
            CalendarGrid.Add(label, i, 0);
        }

        var firstOfMonth = new DateTime(year, month, 1);
        int startDay = (int)firstOfMonth.DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(year, month);
        int row = 1;
        int col = 0;

        for (int i = 0; i < startDay; i++)
        {
            CalendarGrid.Add(new BoxView { Opacity = 0 }, col++, row);
        }

        for (int day = 1; day <= daysInMonth; day++)
        {
            if (col > 6)
            {
                col = 0;
                row++;
            }

            var date = new DateTime(year, month, day);
            bool isSelected = selectedDate == date.Date;
            bool isToday = date.Date == DateTime.Today;
            var dayCell = new Border
            {
                BackgroundColor = GetDayColor(date),
                StrokeThickness = isSelected ? 3 : isToday ? 2 : 1,
                Stroke = isSelected
                    ? ThemeColor("Accent", "Accent")
                    : isToday
                        ? ThemeColor("Accent", "Accent")
                        : ThemeColor("BorderColorLight", "BorderColorDark"),
                HeightRequest = 46,
                Padding = 0
            };
            dayCell.StrokeShape = new RoundRectangle { CornerRadius = 18 };
            dayCell.Content = new Label
            {
                Text = day.ToString(),
                FontAttributes = FontAttributes.Bold,
                TextColor = GetDayTextColor(date),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (_, _) => ShowDayDetails(date);
            dayCell.GestureRecognizers.Add(tapGesture);
            CalendarGrid.Add(dayCell, col++, row);
        }
    }

    private Color GetDayColor(DateTime date)
    {
        if (snapshot is null || !snapshot.Summaries.TryGetValue(date.Date, out var summary))
        {
            if (date.Date == DateTime.Today)
            {
                return ThemeColor("AccentSoftLight", "AccentSoftDark");
            }

            return ThemeColor("SurfaceAltLight", "SurfaceAltDark");
        }

        if (summary.HasIncompleteSession)
        {
            return ThemeColor("WarningSoftLight", "WarningSoftDark");
        }

        if (summary.TotalTime >= snapshot.RequiredTime)
        {
            return ThemeColor("SuccessSoftLight", "SuccessSoftDark");
        }

        return summary.TotalTime > TimeSpan.Zero
            ? ThemeColor("DangerSoftLight", "DangerSoftDark")
            : ThemeColor("SurfaceAltLight", "SurfaceAltDark");
    }

    private Color GetDayTextColor(DateTime date)
    {
        if (snapshot is null || !snapshot.Summaries.TryGetValue(date.Date, out var summary))
        {
            if (date.Date == DateTime.Today)
            {
                return ThemeColor("Accent", "HeroTextDark");
            }

            return ThemeColor("TextSecondaryLight", "TextSecondaryDark");
        }

        if (summary.HasIncompleteSession)
        {
            return ThemeColor("Warning", "Warning");
        }

        if (summary.TotalTime >= snapshot.RequiredTime)
        {
            return ThemeColor("Success", "Success");
        }

        return summary.TotalTime > TimeSpan.Zero
            ? ThemeColor("Danger", "Danger")
            : ThemeColor("TextSecondaryLight", "TextSecondaryDark");
    }

    private void ShowDayDetails(DateTime date)
    {
        selectedDate = date.Date;
        BuildCalendar(currentDate.Year, currentDate.Month);

        if (snapshot is null || !snapshot.Summaries.TryGetValue(date.Date, out var summary))
        {
            SelectedDateLabel.Text = date.ToString("dddd, dd MMM yyyy");
            SelectedMetaLabel.Text = "No office time recorded for this day.";
            DaySessionsCollection.ItemsSource = null;
            return;
        }

        SelectedDateLabel.Text = date.ToString("dddd, dd MMM yyyy");
        SelectedMetaLabel.Text = summary.HasIncompleteSession
            ? $"{FormatDuration(summary.TotalTime)} logged with an unfinished session."
            : $"{FormatDuration(summary.TotalTime)} logged for the day.";

        DaySessionsCollection.ItemsSource = summary.Sessions
            .OrderBy(session => session.InTime)
            .Select(session => new SessionItem
            {
                TimeRange = $"{session.InTime:hh:mm tt} - {(session.OutTime.HasValue ? session.OutTime.Value.ToString("hh:mm tt") : "Open session")}",
                Duration = session.OutTime.HasValue
                    ? $"{FormatDuration(session.OutTime.Value - session.InTime)} in office"
                    : $"{FormatDuration(DateTime.Now - session.InTime)} so far"
            })
            .ToList();
    }

    private void PreviousMonth_Clicked(object sender, EventArgs e)
    {
        currentDate = currentDate.AddMonths(-1);
        BuildCalendar(currentDate.Year, currentDate.Month);
    }

    private void NextMonth_Clicked(object sender, EventArgs e)
    {
        currentDate = currentDate.AddMonths(1);
        BuildCalendar(currentDate.Year, currentDate.Month);
    }

    private static string FormatDuration(TimeSpan timeSpan) => $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes:D2}m";

    private static Color ThemeColor(string lightKey, string darkKey)
    {
        bool isDark = Application.Current?.RequestedTheme == AppTheme.Dark || Application.Current?.UserAppTheme == AppTheme.Dark;
        string key = isDark ? darkKey : lightKey;
        return Application.Current?.Resources[key] as Color ?? Colors.Transparent;
    }

    private sealed class SessionItem
    {
        public string? TimeRange { get; set; }
        public string? Duration { get; set; }
    }
}
