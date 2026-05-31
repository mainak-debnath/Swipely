using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls.Shapes;
using TimeTracker.Helpers;

namespace TimeTracker;

public partial class MainPage : ContentPage
{
    private bool showLogs;
    private bool isRefreshing;
    private List<DateTime> swipeTimes = new();
    private System.Timers.Timer? liveTimer;

    public MainPage()
    {
        InitializeComponent();

        SwipeActionButton.Clicked += OnSwipeAction;
        WeakReferenceMessenger.Default.Register<OfficeHoursUpdatedMessage>(this, async (_, _) => await RefreshAsync(true));
        WeakReferenceMessenger.Default.Register<LogsClearedMessage>(this, async (_, _) => await RefreshAsync(false));
        WeakReferenceMessenger.Default.Register<AllLogsClearedMessage>(this, async (_, _) => await RefreshAsync(false));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync(false);
    }

    private async Task RefreshAsync(bool animateProgress)
    {
        swipeTimes = TimeTrackingService.LoadSwipes();
        await UpdateUiAsync(animateProgress);
    }

    private async void OnSwipeAction(object? sender, EventArgs e)
    {
        swipeTimes.Add(DateTime.Now);
        await TimeTrackingService.SaveSwipesAsync(swipeTimes);
        await UpdateUiAsync(true);
    }

    private async Task UpdateUiAsync(bool animateProgress)
    {
        if (isRefreshing)
        {
            return;
        }

        isRefreshing = true;

        try
        {
            var snapshot = TimeTrackingService.BuildSnapshot(swipeTimes, DateTime.Now);
            WelcomeLabel.Text = DateTime.Now.ToString("dddd, dd MMMM");
            TargetHoursLabel.Text = $"Goal {FormatDuration(snapshot.RequiredTime)}";

            StatusLabel.Text = snapshot.IsCurrentlyInside ? "Currently inside" : "Currently out";
            StatusPill.BackgroundColor = snapshot.IsCurrentlyInside
                ? ThemeColor("SuccessSoftLight", "SuccessSoftDark")
                : ThemeColor("DangerSoftLight", "DangerSoftDark");
            StatusLabel.TextColor = snapshot.IsCurrentlyInside
                ? ThemeColor("Success", "Success")
                : ThemeColor("Danger", "Danger");

            ProgressTimeLabel.Text = $"{FormatDuration(snapshot.TodayTotal)} / {FormatDuration(snapshot.RequiredTime)}";
            PercentageLabel.Text = $"{Math.Min(100, (int)Math.Round((snapshot.TodayTotal.TotalSeconds / snapshot.RequiredTime.TotalSeconds) * 100))}%";
            TimeLeftLabel.Text = snapshot.TodayTotal >= snapshot.RequiredTime
                ? "Daily goal reached"
                : snapshot.IsCurrentlyInside
                    ? $"Time left: {FormatDurationRoundedUp(snapshot.TimeLeft)}"
                    : $"Still needed today: {FormatDurationRoundedUp(snapshot.TimeLeft)}";
            TimeLeftLabel.TextColor = ThemeColor("HeroSubtleLight", "HeroSubtleDark");

            SwipeActionButton.Text = snapshot.IsCurrentlyInside ? "Swipe out" : "Swipe in";
            SwipeActionButton.BackgroundColor = ThemeColor("White", "SurfaceLight");
            SwipeActionButton.TextColor = snapshot.IsCurrentlyInside
                ? ThemeColor("Danger", "Danger")
                : ThemeColor("Accent", "Accent");

            OfficeDaysWeekLabel.Text = $"{snapshot.ActiveDaysThisWeek} / {snapshot.RequiredOfficeDaysPerWeek}";
            OfficeDaysLeftWeekLabel.Text = snapshot.OfficeDaysLeftThisWeek == 0
                ? "Weekly target met"
                : $"{snapshot.OfficeDaysLeftThisWeek} days left";
            WeeklyStreakLabel.Text = FormatWeeks(snapshot.WeeklyStreak);
            BestWeeklyStreakLabel.Text = $"Best: {FormatWeeks(snapshot.BestWeeklyStreak)}";
            GoalDaysLabel.Text = snapshot.GoalDaysThisMonth.ToString();

            if (snapshot.LastSwipeTime.HasValue)
            {
                LastActionLabel.Text = snapshot.LastSwipeWasIn ? "Last swipe was IN" : "Last swipe was OUT";
                LastActionTimeLabel.Text = snapshot.LastSwipeTime.Value.ToString("hh:mm tt");
                LastActionDateLabel.Text = snapshot.LastSwipeTime.Value.ToString("dddd, dd MMM yyyy");
            }
            else
            {
                LastActionLabel.Text = "No swipe recorded yet";
                LastActionTimeLabel.Text = "Start by logging your first office entry.";
                LastActionDateLabel.Text = string.Empty;
            }

            UpdateProgressText(snapshot);
            await UpdateProgressBarAsync(snapshot, animateProgress && !snapshot.IsCurrentlyInside);
            await NotificationService.EvaluateAsync(snapshot);
            TodayLogCollection.ItemsSource = snapshot.TodaySessions.Select(session => new
            {
                Range = $"{session.InTime:hh:mm tt} - {(session.OutTime.HasValue ? session.OutTime.Value.ToString("hh:mm tt") : "Active now")}",
                Detail = session.OutTime.HasValue
                    ? $"{FormatDuration(session.OutTime.Value - session.InTime)} in office"
                    : $"{FormatDuration(DateTime.Now - session.InTime)} and counting",
                Tone = session.OutTime.HasValue
                    ? ThemeColor("AccentSoftLight", "HeroMidDark")
                    : ThemeColor("SuccessSoftLight", "SuccessSoftDark"),
                ToneText = session.OutTime.HasValue
                    ? ThemeColor("Accent", "White")
                    : ThemeColor("Success", "Success")
            }).ToList();

            TodayLogCollection.ItemTemplate = new DataTemplate(() =>
            {
                var card = new Border
                {
                    Padding = new Thickness(14),
                    Margin = new Thickness(0, 0, 0, 10),
                    BackgroundColor = ThemeColor("SurfaceLight", "SurfaceDark")
                };
                card.StrokeShape = new RoundRectangle { CornerRadius = 18 };

                var range = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold };
                range.SetBinding(Label.TextProperty, "Range");

                var detail = new Label { FontSize = 13, TextColor = ThemeColor("TextSecondaryLight", "TextSecondaryDark") };
                detail.SetBinding(Label.TextProperty, "Detail");

                var tone = new Border
                {
                    Padding = new Thickness(10, 4),
                    StrokeThickness = 0,
                    HorizontalOptions = LayoutOptions.Start
                };
                tone.StrokeShape = new RoundRectangle { CornerRadius = 999 };
                tone.SetBinding(BackgroundColorProperty, "Tone");

                var toneText = new Label { FontSize = 12, FontAttributes = FontAttributes.Bold };
                toneText.SetBinding(Label.TextProperty, "Detail");
                toneText.SetBinding(Label.TextColorProperty, "ToneText");
                tone.Content = toneText;

                card.Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children = { range, detail, tone }
                };

                return card;
            });

            if (snapshot.IsCurrentlyInside)
            {
                StartLiveTimer();
            }
            else
            {
                StopLiveTimer();
            }
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private void UpdateProgressText(TrackingSnapshot snapshot)
    {
        var ratio = snapshot.TodayTotal.TotalSeconds / snapshot.RequiredTime.TotalSeconds;

        if (ratio >= 1)
        {
            ProgressStatusLabel.Text = "Goal achieved. Anything from here is extra.";
            ProgressStatusLabel.TextColor = ThemeColor("HeroSubtleLight", "HeroSubtleDark");
        }
        else if (snapshot.IsCurrentlyInside)
        {
            ProgressStatusLabel.Text = "Live tracking is active right now.";
            ProgressStatusLabel.TextColor = ThemeColor("HeroSubtleLight", "HeroSubtleDark");
        }
        else if (snapshot.TodayTotal > TimeSpan.Zero)
        {
            ProgressStatusLabel.Text = $"{FormatDurationRoundedUp(snapshot.TimeLeft)} left to hit today's target.";
            ProgressStatusLabel.TextColor = ThemeColor("HeroSubtleLight", "HeroSubtleDark");
        }
        else
        {
            ProgressStatusLabel.Text = "Ready to start your day.";
            ProgressStatusLabel.TextColor = ThemeColor("HeroSubtleLight", "HeroSubtleDark");
        }
    }

    private async Task UpdateProgressBarAsync(TrackingSnapshot snapshot, bool animate)
    {
        var progress = Math.Clamp(snapshot.TodayTotal.TotalSeconds / snapshot.RequiredTime.TotalSeconds, 0, 1);
        var targetWidth = (ProgressTrack.Width <= 0 ? ProgressTrack.WidthRequest : ProgressTrack.Width) * progress;
        ProgressFill.BackgroundColor = snapshot.TodayTotal >= snapshot.RequiredTime
            ? ThemeColor("SuccessSoftLight", "SuccessBrightDark")
            : ThemeColor("White", "SurfaceLight");

        if (animate)
        {
            await ProgressFill.LayoutTo(new Rect(0, 0, targetWidth, ProgressTrack.Height <= 0 ? 14 : ProgressTrack.Height), 280, Easing.CubicOut);
        }
        else
        {
            ProgressFill.WidthRequest = targetWidth;
        }
    }

    private void StartLiveTimer()
    {
        if (liveTimer is not null)
        {
            return;
        }

        liveTimer = new System.Timers.Timer(1000);
        liveTimer.Elapsed += async (_, _) =>
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () => await UpdateUiAsync(false));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Timer error: {ex.Message}");
            }
        };
        liveTimer.Start();
    }

    private void StopLiveTimer()
    {
        if (liveTimer is null)
        {
            return;
        }

        liveTimer.Stop();
        liveTimer.Dispose();
        liveTimer = null;
    }

    private async void ToggleLogsButton_Clicked(object sender, EventArgs e)
    {
        showLogs = !showLogs;
        ToggleLogsButton.Text = showLogs ? "Hide" : "Show";

        if (showLogs)
        {
            LogsSection.IsVisible = true;
            LogsSection.Opacity = 0;
            LogsSection.TranslationY = -8;
            await Task.WhenAll(
                LogsSection.FadeTo(1, 220),
                LogsSection.TranslateTo(0, 0, 220, Easing.CubicOut));
        }
        else
        {
            await Task.WhenAll(
                LogsSection.FadeTo(0, 180),
                LogsSection.TranslateTo(0, -8, 180, Easing.CubicIn));
            LogsSection.IsVisible = false;
        }
    }

    private async void WeeklyStreakInfo_Tapped(object sender, TappedEventArgs e)
    {
        var weeklyGoal = TimeTrackingService.GetOfficeDaysPerWeekGoal();
        WeeklyStreakInfoSummaryLabel.Text = $"Complete {weeklyGoal} office days in a week to keep your streak alive.";
        WeeklyStreakInfoTargetLabel.Text = $"A week counts when you complete your {weeklyGoal}-day office target. You can change this target from Settings anytime.";

        WeeklyStreakInfoOverlay.IsVisible = true;
        WeeklyStreakInfoOverlay.Opacity = 0;
        WeeklyStreakInfoCard.Scale = 0.94;
        WeeklyStreakInfoCard.TranslationY = 18;

        await Task.WhenAll(
            WeeklyStreakInfoOverlay.FadeTo(1, 180, Easing.CubicOut),
            WeeklyStreakInfoCard.ScaleTo(1, 260, Easing.CubicOut),
            WeeklyStreakInfoCard.TranslateTo(0, 0, 260, Easing.CubicOut));
    }

    private async void WeeklyStreakInfoClose_Clicked(object sender, EventArgs e)
    {
        if (!WeeklyStreakInfoOverlay.IsVisible)
        {
            return;
        }

        await Task.WhenAll(
            WeeklyStreakInfoOverlay.FadeTo(0, 150, Easing.CubicIn),
            WeeklyStreakInfoCard.ScaleTo(0.96, 150, Easing.CubicIn),
            WeeklyStreakInfoCard.TranslateTo(0, 12, 150, Easing.CubicIn));

        WeeklyStreakInfoOverlay.IsVisible = false;
        WeeklyStreakInfoCard.Scale = 1;
        WeeklyStreakInfoCard.TranslationY = 0;
    }

    private static string FormatDuration(TimeSpan timeSpan) => $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes:D2}m";

    private static string FormatDurationRoundedUp(TimeSpan timeSpan)
    {
        if (timeSpan <= TimeSpan.Zero)
        {
            return "0h 00m";
        }

        var rounded = TimeSpan.FromMinutes(Math.Ceiling(timeSpan.TotalMinutes));
        return FormatDuration(rounded);
    }

    private static string FormatWeeks(int weeks) => weeks == 1 ? "1 week" : $"{weeks} weeks";

    private static Color ThemeColor(string lightKey, string darkKey)
    {
        bool isDark = Application.Current?.RequestedTheme == AppTheme.Dark || Application.Current?.UserAppTheme == AppTheme.Dark;
        string key = isDark ? darkKey : lightKey;
        return Application.Current?.Resources[key] as Color ?? Colors.Transparent;
    }
}
