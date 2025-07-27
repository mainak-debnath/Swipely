using CommunityToolkit.Mvvm.Messaging;
using System.Text.Json;
using System.Timers;
using TimeTracker.Helpers;

namespace TimeTracker
{
    public partial class MainPage : ContentPage
    {
        private bool showLogs = false;
        private bool isLiveUpdating = false;

        private TimeSpan RequiredTime => TimeSpan.FromHours(
            Preferences.Get("OfficeHoursGoal", 5.0)
        );
        private List<DateTime> swipeTimes = new();
        private string storagePath => Path.Combine(FileSystem.AppDataDirectory, "swipes.json");

        private System.Timers.Timer? liveTimer;

        public MainPage()
        {
            InitializeComponent();
            WeakReferenceMessenger.Default.Register<OfficeHoursUpdatedMessage>(this, async (r, message) =>
            {
                await UpdateUI();
            });
            WeakReferenceMessenger.Default.Register<LogsClearedMessage>(this, async (r, message) =>
            {
                LoadSwipeData();
                await UpdateUI();

                // If today's data was cleared, we might need to stop the timer
                if (message.Year == DateTime.Today.Year && message.Month == DateTime.Today.Month)
                {
                    StopLiveTimer();
                }
            });

            WeakReferenceMessenger.Default.Register<AllLogsClearedMessage>(this, async (r, message) =>
            {
                LoadSwipeData();
                await UpdateUI();
                StopLiveTimer();
            });
            LoadSwipeData();
            MainThread.InvokeOnMainThreadAsync(async () => await UpdateUI());


            SwipeActionButton.Clicked += OnSwipeAction;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await UpdateUI();
        }

        private async void OnSwipeAction(object sender, EventArgs e)
        {
            swipeTimes.Add(DateTime.Now);
            await UpdateUI();
            await SaveSwipeDataAsync();
        }

        private async void ToggleLogsButton_Clicked(object sender, EventArgs e)
        {
            showLogs = !showLogs;
            ToggleLogsButton.Text = showLogs ? "▲ Hide Today's Activity" : "▼ Show Today's Activity";
            ToggleLogsButton.BackgroundColor = showLogs ? Color.FromArgb("#D9480F") : Color.FromArgb("#E85D04");
            if (showLogs)
            {
                LogsSection.IsVisible = true;
                LogsSection.Opacity = 0;
                LogsSection.TranslationY = -10;

                // Animate in
                await Task.WhenAll(
                    LogsSection.FadeTo(1, 250),
                    LogsSection.TranslateTo(0, 0, 250, Easing.CubicOut)
                );
            }
            else
            {
                // Animate out
                await Task.WhenAll(
                    LogsSection.FadeTo(0, 250),
                    LogsSection.TranslateTo(0, -10, 250, Easing.CubicIn)
                );
                LogsSection.IsVisible = false;
            }
        }


        private async Task UpdateUI()
        {
            if (isLiveUpdating) return;

            isLiveUpdating = true;
            // Use ALL swipes to determine if inside (original logic was correct)
            try
            {
                bool isInside = swipeTimes.Count % 2 != 0;

                // Calculate today's total time using the SAME logic as CalendarPage
                var today = DateTime.Today;
                TimeSpan totalInside = TimeSpan.Zero;
                bool hasActiveSessionToday = false;

                // Process swipes in pairs, just like CalendarPage
                for (int i = 0; i < swipeTimes.Count; i += 2)
                {
                    var inTime = swipeTimes[i];

                    // Skip if not today
                    if (inTime.Date != today) continue;

                    if (i + 1 < swipeTimes.Count)
                    {
                        // Complete session (has OUT swipe)
                        var outTime = swipeTimes[i + 1];

                        // Make sure OUT is also today
                        if (outTime.Date == today)
                        {
                            totalInside += outTime - inTime;
                        }
                    }
                    else
                    {
                        // Active session (no OUT swipe yet) - this means we're currently inside
                        var currentTime = DateTime.Now;
                        var activeDuration = currentTime - inTime;
                        totalInside += activeDuration;
                        hasActiveSessionToday = true;
                    }
                }

                // Start/stop timer based on whether we have an active session TODAY
                if (hasActiveSessionToday)
                {
                    StartLiveTimer();
                }
                else
                {
                    StopLiveTimer();
                }

                await UpdateLinearProgress(totalInside, hasActiveSessionToday);

                // Calculate Time Left
                var timeLeft = RequiredTime - totalInside;
                if (timeLeft < TimeSpan.Zero) timeLeft = TimeSpan.Zero;

                // Update UI Elements
                StatusLabel.Text = isInside ? "🟢 Currently IN" : "🔴 Currently OUT";
                ((Frame)StatusLabel.Parent).BackgroundColor = isInside ? Colors.LightGreen : Colors.IndianRed;

                // Time Left Message
                if (!isInside)
                    TimeLeftLabel.Text = "Ready for your next session!";
                else if (totalInside >= RequiredTime)
                    TimeLeftLabel.Text = "Target Met!";
                else
                    TimeLeftLabel.Text = $"Time Left: {(int)timeLeft.TotalHours}h {timeLeft.Minutes}m";
                TimeLeftLabel.TextColor = totalInside >= RequiredTime ? Colors.Green : Colors.DarkRed;

                // Swipe Button
                SwipeActionButton.Text = isInside ? "Swipe OUT" : "Swipe IN";
                SwipeActionButton.BackgroundColor = isInside ? Colors.Red : Colors.Green;

                // Show last action ONLY for today's swipes
                var lastTodaySwipe = swipeTimes.LastOrDefault(s => s.Date == today);

                if (lastTodaySwipe != default)
                {
                    LastActionLabel.Text = isInside ? "Last Action: Swipe IN" : "Last Action: Swipe OUT";
                    LastActionTimeLabel.Text = $"Time: {lastTodaySwipe:hh:mm tt}";
                    LastActionDateLabel.Text = $"Date: {lastTodaySwipe:dd MMM yyyy}";
                }
                else
                {
                    LastActionLabel.Text = "No previous actions";
                    LastActionTimeLabel.Text = "";
                    LastActionDateLabel.Text = "";
                }


                // Populate Today Log using the same logic as CalendarPage
                var todayLogs = new List<SwipeLogItem>();
                for (int i = 0; i < swipeTimes.Count; i += 2)
                {
                    var inTime = swipeTimes[i];
                    if (inTime.Date != today) continue;

                    var log = new SwipeLogItem { InTime = inTime };

                    if (i + 1 < swipeTimes.Count && swipeTimes[i + 1].Date == today)
                    {
                        log.OutTime = swipeTimes[i + 1];
                    }

                    todayLogs.Add(log);
                }

                TodayLogCollection.ItemsSource = todayLogs.Select(log => new
                {
                    In = $"IN: {log.InTime:hh:mm tt}",
                    Out = log.OutTime.HasValue ? $"OUT: {log.OutTime:hh:mm tt} ({(log.OutTime.Value - log.InTime).Hours}h {(log.OutTime.Value - log.InTime).Minutes}m)" : "Currently Active",
                    Label = log.OutTime.HasValue ? "" : "(Active)"
                });
            }
            finally
            {
                isLiveUpdating = false;
            }
        }

        private void StartLiveTimer()
        {
            if (liveTimer != null) return;

            liveTimer = new System.Timers.Timer(1000);
            liveTimer.Elapsed += async (s, e) =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await UpdateUI();
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Timer error: {ex.Message}");
                }
            };
            liveTimer.Start();
        }

        private void StopLiveTimer()
        {
            if (liveTimer != null)
            {
                liveTimer.Stop();
                liveTimer.Dispose();
                liveTimer = null;
            }
        }

        private async Task SaveSwipeDataAsync()
        {
            var json = JsonSerializer.Serialize(swipeTimes);
            await File.WriteAllTextAsync(storagePath, json);
        }

        private void LoadSwipeData()
        {
            if (File.Exists(storagePath))
            {
                string json = File.ReadAllText(storagePath);
                swipeTimes = JsonSerializer.Deserialize<List<DateTime>>(json) ?? new();
            }
            else
            {
                swipeTimes = new();
            }
        }

        private class SwipeLogItem
        {
            public DateTime InTime { get; set; }
            public DateTime? OutTime { get; set; }
        }
        private async Task UpdateLinearProgress(TimeSpan totalInside, bool animate = true)
        {
            var progressValue = totalInside.TotalSeconds / RequiredTime.TotalSeconds;
            var clampedProgress = Math.Clamp(progressValue, 0, 1);
            var percentage = (int)(clampedProgress * 100);

            // Calculate target width (300 is the total width of the progress bar)
            var targetWidth = 300 * clampedProgress;

            // Update the time display
            ProgressTimeLabel.Text = $"{(int)totalInside.TotalHours}h {totalInside.Minutes:D2}m / {(int)RequiredTime.TotalHours}h {RequiredTime.Minutes:D2}m";

            // Update percentage
            PercentageLabel.Text = $"{percentage}%";

            // Update status text
            if (clampedProgress >= 1.0)
            {
                ProgressStatusLabel.Text = "🎉 Goal achieved!";
                ProgressStatusLabel.TextColor = Colors.Green;
                ProgressFill.BackgroundColor = Color.FromArgb("#DFC037");
            }
            else if (clampedProgress >= 0.8)
            {
                ProgressStatusLabel.Text = "Almost there!";
                ProgressStatusLabel.TextColor = Colors.Orange;
                ProgressFill.BackgroundColor = Color.FromArgb("#4CAF50"); // Green
            }
            else if (clampedProgress > 0)
            {
                var timeLeft = RequiredTime - totalInside;
                ProgressStatusLabel.Text = $"{(int)timeLeft.TotalHours}h {timeLeft.Minutes}m remaining";
                ProgressStatusLabel.TextColor = Colors.Gray;
                ProgressFill.BackgroundColor = Color.FromArgb("#4CAF50"); // Green
            }
            else
            {
                ProgressStatusLabel.Text = "Ready to start!";
                ProgressStatusLabel.TextColor = Colors.Gray;
            }

            // Animate the progress fill width
            if (animate && !isLiveUpdating) // Don't animate during live updates
            {
                await ProgressFill.LayoutTo(new Rect(0, 0, targetWidth, 30), 300, Easing.CubicOut);
            }
            else
            {
                // Direct update for live timer
                ProgressFill.WidthRequest = targetWidth;
            }
        }
    }
}

