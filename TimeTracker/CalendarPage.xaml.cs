using CommunityToolkit.Mvvm.Messaging;
using System.Text.Json;
using TimeTracker.Helpers;
using System.Linq;

namespace TimeTracker
{
    public partial class CalendarPage : ContentPage
    {
        private Dictionary<DateTime, TimeSpan> dailyTotals = new();
        private string storagePath => Path.Combine(FileSystem.AppDataDirectory, "swipes.json");
        private HashSet<DateTime> forgottenCheckoutDates = new();

        private TimeSpan RequiredTime => TimeSpan.FromHours(
            Preferences.Get("OfficeHoursGoal", 5.0)
        );

        private DateTime _currentDate;

        public CalendarPage()
        {
            InitializeComponent();

            // Subscribe to office hours updates (same as MainPage)
            WeakReferenceMessenger.Default.Register<OfficeHoursUpdatedMessage>(this, async (r, message) =>
            {
                LoadSwipeData(); // Recalculate data
                BuildCalendar(_currentDate.Year, _currentDate.Month); // Rebuild calendar with new colors
            });
            WeakReferenceMessenger.Default.Register<LogsClearedMessage>(this, async (r, message) =>
            {
                LoadSwipeData(); // Reload all data from file
                BuildCalendar(_currentDate.Year, _currentDate.Month); // Rebuild calendar

                // Clear selected day details if viewing the cleared month
                if (_currentDate.Year == message.Year && _currentDate.Month == message.Month)
                {
                    SelectedDateLabel.Text = "";
                    DaySessionsCollection.ItemsSource = null;
                }
            });

            WeakReferenceMessenger.Default.Register<AllLogsClearedMessage>(this, async (r, message) =>
            {
                LoadSwipeData(); // Reload all data (will be empty)
                BuildCalendar(_currentDate.Year, _currentDate.Month); // Rebuild calendar
                SelectedDateLabel.Text = "";
                DaySessionsCollection.ItemsSource = null;
            });
            LoadSwipeData();

            // 2. Initialize the date to today and build the initial calendar
            _currentDate = DateTime.Today;
            BuildCalendar(_currentDate.Year, _currentDate.Month);
        }

        // Add a dictionary to track days with incomplete sessions
        private HashSet<DateTime> incompleteDays = new();

        private void LoadSwipeData()
        {
            dailyTotals.Clear();
            incompleteDays.Clear();

            if (!File.Exists(storagePath))
                return;

            var json = File.ReadAllText(storagePath);
            var allSwipes = JsonSerializer.Deserialize<List<DateTime>>(json) ?? new();

            // Group all swipes by their date to process each day individually
            var swipesByDay = allSwipes
                .GroupBy(swipe => swipe.Date)
                .ToDictionary(group => group.Key, group => group.OrderBy(swipe => swipe).ToList());

            // Process each day's data
            foreach (var dayEntry in swipesByDay)
            {
                var date = dayEntry.Key;
                var daySwipes = dayEntry.Value;
                var totalDurationForDay = TimeSpan.Zero;

                // Process all COMPLETE pairs of swipes for the day
                for (int i = 0; i < daySwipes.Count - 1; i += 2)
                {
                    totalDurationForDay += daySwipes[i + 1] - daySwipes[i];
                }

                // NOW, check if the day is incomplete (an odd number of swipes)
                if (daySwipes.Count % 2 != 0)
                {
                    // This day is incomplete, so add it to the set for the yellow color
                    incompleteDays.Add(date);
                    var lastInTime = daySwipes.Last(); // The unpaired swipe

                    if (date.Date < DateTime.Today)
                    {
                        // Forgotten logout from a previous day - cap at midnight
                        var endOfDay = date.Date.AddDays(1).AddTicks(-1);
                        totalDurationForDay += endOfDay - lastInTime;
                    }
                    else
                    {
                        // Active session for today
                        totalDurationForDay += DateTime.Now - lastInTime;
                    }
                }

                // Store the final calculated total for the day
                dailyTotals[date] = totalDurationForDay;
            }
        }

        private void BuildCalendar(int year, int month)
        {
            // 3. Update the Month/Year label
            MonthYearLabel.Text = new DateTime(year, month, 1).ToString("MMMM yyyy");

            CalendarGrid.RowDefinitions.Clear();
            CalendarGrid.ColumnDefinitions.Clear();
            CalendarGrid.Children.Clear();

            // Add weekday headers
            for (int i = 0; i < 7; i++)
            {
                CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }

            // Add rows
            for (int i = 0; i < 7; i++)
            {
                CalendarGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            string[] daysOfWeek = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            for (int i = 0; i < 7; i++)
            {
                var lbl = new Label
                {
                    Text = daysOfWeek[i],
                    HorizontalOptions = LayoutOptions.Center,
                    FontAttributes = FontAttributes.Bold
                };
                CalendarGrid.Add(lbl, i, 0);
            }

            var firstOfMonth = new DateTime(year, month, 1);
            int startDay = (int)firstOfMonth.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(year, month);
            int currentRow = 1;
            int currentCol = 0;

            for (int i = 0; i < startDay; i++)
            {
                var emptyBox = new BoxView { Color = Colors.Transparent };
                CalendarGrid.Add(emptyBox, currentCol++, currentRow);
            }

            for (int day = 1; day <= daysInMonth; day++)
            {
                if (currentCol > 6)
                {
                    currentCol = 0;
                    currentRow++;
                }

                var date = new DateTime(year, month, day);
                var buttonDate = date;

                var btn = new Button
                {
                    Text = day.ToString(),
                    BackgroundColor = GetDayColor(date),
                    TextColor = Colors.Black,
                    CornerRadius = 5 // Adjusted CornerRadius
                    // Removed fixed WidthRequest and HeightRequest
                };

                btn.Clicked += (s, e) => ShowDayDetails(buttonDate);
                CalendarGrid.Add(btn, currentCol++, currentRow);
            }
        }

        private Color GetDayColor(DateTime date)
        {
            // Rule 1: If the day has no final logout, it's always yellow.
            if (incompleteDays.Contains(date.Date))
            {
                return Color.FromArgb("#DFC037");
            }

            // Check completed days
            if (dailyTotals.TryGetValue(date.Date, out var duration) && duration > TimeSpan.Zero)
            {
                // Rule 2 & 3: Is the goal met for this completed day?
                return duration >= RequiredTime ? Colors.LightGreen : Colors.IndianRed;
            }

            // Default for days with no data
            return Colors.LightGray;
        }

        private void ShowDayDetails(DateTime date)
        {
            SelectedDateLabel.Text = $"Details for {date:dd MMM yyyy}";

            if (!File.Exists(storagePath))
            {
                DaySessionsCollection.ItemsSource = null;
                return;
            }

            var swipes = JsonSerializer.Deserialize<List<DateTime>>(File.ReadAllText(storagePath)) ?? new();
            var sessionItems = new List<SessionItem>();
            TimeSpan totalForDay = TimeSpan.Zero;
            var daySwipes = swipes.Where(s => s.Date == date.Date).OrderBy(s => s).ToList();
            for (int i = 0; i < daySwipes.Count; i += 2)
            {
                var inTime = daySwipes[i];
                if (i + 1 < daySwipes.Count)
                {
                    var outTime = daySwipes[i + 1];
                    var duration = outTime - inTime;
                    sessionItems.Add(new SessionItem
                    {
                        TimeRange = $"IN: {inTime:hh:mm tt}   OUT: {outTime:hh:mm tt}",
                        Duration = $"Duration: {(int)duration.TotalHours}h {duration.Minutes}m"
                    });
                    totalForDay += duration;
                }
                else // This is the last, unpaired swipe
                {
                    string timeRangeText;
                    string durationText;
                    TimeSpan activeDuration;
                    if (date.Date < DateTime.Today)
                    {
                        // Forgotten logout from a past day
                        activeDuration = date.AddDays(1).AddTicks(-1) - inTime;
                        timeRangeText = $"IN: {inTime:hh:mm tt}   OUT: -- (Forgot)";
                        durationText = $"Capped at Midnight: {(int)activeDuration.TotalHours}h {activeDuration.Minutes}m";
                    }
                    else
                    {
                        // Active session for today
                        activeDuration = DateTime.Now - inTime;
                        timeRangeText = $"IN: {inTime:hh:mm tt}   OUT: --";
                        durationText = $"Currently Inside: {(int)activeDuration.TotalHours}h {activeDuration.Minutes}m";
                    }
                    sessionItems.Add(new SessionItem
                    {
                        TimeRange = timeRangeText,
                        Duration = durationText
                    });
                    totalForDay += activeDuration;
                }
            }

            DaySessionsCollection.ItemsSource = sessionItems;
            string statusText;

            // Check if the day is marked as incomplete by LoadSwipeData method
            if (incompleteDays.Contains(date.Date))
            {
                statusText = "❗ Incomplete (No Logout)";
            }
            else
            {
                if (totalForDay == TimeSpan.Zero)
                {
                    statusText = "No time recorded";
                }
                else
                {
                    statusText = totalForDay >= RequiredTime ? "✅ Goal Met" : "❌ Goal Not Met";
                }
            }

            // Update total time in header label with the correct status
            SelectedDateLabel.Text = $"Details for {date:dd MMM yyyy} | Total: {(int)totalForDay.TotalHours}h {totalForDay.Minutes}m | {statusText}";
        }

        // Helper class
        private class SessionItem
        {
            public string? TimeRange { get; set; }
            public string? Duration { get; set; }
        }

        // 4. Add the event handlers for the navigation buttons
        private void PreviousMonth_Clicked(object sender, EventArgs e)
        {
            _currentDate = _currentDate.AddMonths(-1);
            BuildCalendar(_currentDate.Year, _currentDate.Month);
            SelectedDateLabel.Text = "";
            DaySessionsCollection.ItemsSource = null;
        }

        private void NextMonth_Clicked(object sender, EventArgs e)
        {
            _currentDate = _currentDate.AddMonths(1);
            BuildCalendar(_currentDate.Year, _currentDate.Month);
            SelectedDateLabel.Text = "";
            DaySessionsCollection.ItemsSource = null;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }
    }
}
