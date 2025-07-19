using System.Text.Json;

namespace TimeTracker
{
    public partial class CalendarPage : ContentPage
    {
        private Dictionary<DateTime, TimeSpan> dailyTotals = new();
        private string storagePath => Path.Combine(FileSystem.AppDataDirectory, "swipes.json");

        // 1. Add a state variable to track the current month and year
        private DateTime _currentDate;

        public CalendarPage()
        {
            InitializeComponent();
            LoadSwipeData();

            // 2. Initialize the date to today and build the initial calendar
            _currentDate = DateTime.Today;
            BuildCalendar(_currentDate.Year, _currentDate.Month);
        }

        private void LoadSwipeData()
        {
            if (!File.Exists(storagePath))
                return;

            var json = File.ReadAllText(storagePath);
            var swipes = JsonSerializer.Deserialize<List<DateTime>>(json) ?? new();

            dailyTotals.Clear();

            for (int i = 0; i < swipes.Count; i += 2)
            {
                var inTime = swipes[i];

                if (i + 1 < swipes.Count)
                {
                    // Complete session (has OUT swipe)
                    var outTime = swipes[i + 1];

                    if (inTime.Date != outTime.Date) continue;

                    if (!dailyTotals.ContainsKey(inTime.Date))
                        dailyTotals[inTime.Date] = TimeSpan.Zero;

                    dailyTotals[inTime.Date] += outTime - inTime;
                }
                else
                {
                    // Active session (no OUT swipe yet)
                    var currentTime = DateTime.Now;
                    var activeDuration = currentTime - inTime;

                    if (!dailyTotals.ContainsKey(inTime.Date))
                        dailyTotals[inTime.Date] = TimeSpan.Zero;

                    dailyTotals[inTime.Date] += activeDuration;
                }
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
            if (dailyTotals.TryGetValue(date.Date, out var duration))
            {
                if (duration.TotalHours >= 5)
                    return Colors.LightGreen;
                else if (duration.TotalHours >= 4)
                    return Colors.Khaki;
                else
                    return Colors.IndianRed;
            }
            return Colors.LightGray; // Use LightGray for better visibility than Transparent
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

            for (int i = 0; i < swipes.Count; i += 2)
            {
                if (swipes[i].Date != date.Date)
                    continue;

                var inTime = swipes[i];
                DateTime? outTime = (i + 1 < swipes.Count && swipes[i + 1].Date == date.Date) ? swipes[i + 1] : null;

                if (outTime.HasValue)
                {
                    var duration = outTime.Value - inTime;
                    sessionItems.Add(new SessionItem
                    {
                        TimeRange = $"IN: {inTime:hh:mm tt}   OUT: {outTime.Value:hh:mm tt}",
                        Duration = $"Duration: {(int)duration.TotalHours}h {duration.Minutes}m"
                    });
                    totalForDay += duration;
                }
                else
                {
                    // For active sessions, calculate time from IN to current time
                    var currentTime = DateTime.Now;
                    var activeDuration = currentTime - inTime;

                    sessionItems.Add(new SessionItem
                    {
                        TimeRange = $"IN: {inTime:hh:mm tt}   OUT: --",
                        Duration = $"Currently Inside ({(int)activeDuration.TotalHours}h {activeDuration.Minutes}m)"
                    });

                    // Add the active session time to total
                    totalForDay += activeDuration;
                }
            }

            DaySessionsCollection.ItemsSource = sessionItems;

            // Update total time in header label
            SelectedDateLabel.Text = $"Details for {date:dd MMM yyyy} | Total: {(int)totalForDay.TotalHours}h {totalForDay.Minutes}m";
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
    }
}

