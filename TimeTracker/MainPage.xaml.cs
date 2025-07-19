using CommunityToolkit.Mvvm.Messaging;
using System.Text.Json;
using System.Timers;
using TimeTracker.Helpers;

namespace TimeTracker
{
    public partial class MainPage : ContentPage
    {
        private bool showLogs = false;

        private TimeSpan RequiredTime => TimeSpan.FromHours(
            Preferences.Get("OfficeHoursGoal", 5.0)
        );
        private List<DateTime> swipeTimes = new();
        private string storagePath => Path.Combine(FileSystem.AppDataDirectory, "swipes.json");

        private System.Timers.Timer? liveTimer;
        private CircularProgressBarDrawable progressBar;

        public MainPage()
        {
            InitializeComponent();
            WeakReferenceMessenger.Default.Register<OfficeHoursUpdatedMessage>(this, async (r, message) =>
            {
                await UpdateUI();
            });
            // Initialize the progress bar and assign it
            progressBar = new CircularProgressBarDrawable();
            ProgressBarView.Drawable = progressBar;
            LoadSwipeData();
            MainThread.InvokeOnMainThreadAsync(async () => await UpdateUI());


            SwipeActionButton.Clicked += OnSwipeAction;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await UpdateUI(); // This will refresh every time the page appears
        }

        private async void OnSwipeAction(object sender, EventArgs e)
        {
            swipeTimes.Add(DateTime.Now);
            SaveSwipeData();
            await UpdateUI();
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
            // Use ALL swipes to determine if inside (original logic was correct)
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

            // Update Progress Bar
            var progressValue = (float)(totalInside.TotalSeconds / RequiredTime.TotalSeconds);

            progressBar.TrackColor = Colors.Gray;
            progressBar.ProgressColor = Colors.Green;

            // Don't await animation during live updates to prevent blocking
            var clampedProgress = Math.Clamp(progressValue, 0, 1);
            if (liveTimer != null)
            {
                // During live updates, just set progress directly
                progressBar.Progress = clampedProgress;
                ProgressBarView.Invalidate();
            }
            else
            {
                // Only animate when not in live mode
                await AnimateProgressBar(clampedProgress);
            }

            // Calculate Time Left
            var timeLeft = RequiredTime - totalInside;
            if (timeLeft < TimeSpan.Zero) timeLeft = TimeSpan.Zero;

            // Update UI Elements
            StatusLabel.Text = isInside ? "🟢 Currently IN" : "🔴 Currently OUT";
            ((Frame)StatusLabel.Parent).BackgroundColor = isInside ? Colors.LightGreen : Colors.IndianRed;

            // Progress Text
            ProgressTimeLabel.Text = $"{(int)totalInside.TotalHours}h {totalInside.Minutes}m / {(int)RequiredTime.TotalHours}h {RequiredTime.Minutes}m";

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

            // Last Action Info - use ALL swipes for last action (original logic)
            if (swipeTimes.Any())
            {
                var lastTime = swipeTimes[^1];
                LastActionLabel.Text = isInside ? "Last Action: Swipe IN" : "Last Action: Swipe OUT";
                LastActionTimeLabel.Text = $"Time: {lastTime:hh:mm tt}";
                LastActionDateLabel.Text = $"Date: {lastTime:dd MMM yyyy}";
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

        private void SaveSwipeData()
        {
            File.WriteAllText(storagePath, JsonSerializer.Serialize(swipeTimes));
        }

        private void LoadSwipeData()
        {
            if (File.Exists(storagePath))
            {
                string json = File.ReadAllText(storagePath);
                swipeTimes = JsonSerializer.Deserialize<List<DateTime>>(json) ?? new();
            }
        }

        private async Task AnimateProgressBar(float newProgress)
        {
            // Skip animation if progress hasn't changed much or if we're doing live updates
            if (Math.Abs(newProgress - progressBar.Progress) < 0.01f)
            {
                progressBar.Progress = newProgress;
                ProgressBarView.Invalidate();
                return;
            }

            float oldProgress = progressBar.Progress;
            float duration = 300; // milliseconds
            int frameRate = 30;
            int totalFrames = (int)(duration / 1000 * frameRate);

            if (totalFrames == 0) totalFrames = 1; // Prevent division by zero

            float increment = (newProgress - oldProgress) / totalFrames;

            for (int i = 0; i < totalFrames; i++)
            {
                progressBar.Progress += increment;
                ProgressBarView.Invalidate();
                await Task.Delay((int)(1000 / frameRate));
            }

            progressBar.Progress = newProgress; // Final snap
            ProgressBarView.Invalidate();
        }


        private class SwipeLogItem
        {
            public DateTime InTime { get; set; }
            public DateTime? OutTime { get; set; }
        }

        public class CircularProgressBarDrawable : IDrawable
        {
            public float Progress { get; set; } = 0;
            public Color ProgressColor { get; set; } = Colors.Gray;
            public Color TrackColor { get; set; } = Colors.Green;

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                canvas.SaveState();

                float size = Math.Min(dirtyRect.Width, dirtyRect.Height);
                float strokeWidth = size * 0.1f; 
                float radius = (size / 2) - (strokeWidth / 2);
                canvas.StrokeSize = strokeWidth;
                canvas.StrokeLineCap = LineCap.Round;

                var arcRect = new RectF(
                    dirtyRect.Left + strokeWidth / 2,
                    dirtyRect.Top + strokeWidth / 2,
                    size - strokeWidth,
                    size - strokeWidth
                );

                // 1. Always draw the full gray circle as background
                canvas.StrokeColor = TrackColor;
                canvas.DrawCircle(dirtyRect.Center.X, dirtyRect.Center.Y, radius);

                // 2. Draw green arc on top based on progress
                if (Progress > 0)
                {
                    float clampedProgress = Math.Min(Progress, 1.0f); // Clamp to prevent overdraw
                    float progressAngle = 360f * clampedProgress;

                    canvas.StrokeColor = ProgressColor;
                    canvas.DrawArc(arcRect, 90f, progressAngle, false, false);
                }

                canvas.RestoreState();
            }
        }
    }
}

