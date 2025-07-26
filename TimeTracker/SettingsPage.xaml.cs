using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls.Shapes;
using System.Text.Json;
using TimeTracker.Helpers;

namespace TimeTracker
{
    public partial class SettingsPage : ContentPage
    {
        const string OfficeHoursKey = "OfficeHoursGoal";
        private string storagePath => System.IO.Path.Combine(FileSystem.AppDataDirectory, "swipes.json");
        private DateTime? selectedMonth = null;

        public SettingsPage()
        {
            InitializeComponent();
            double savedHours = Preferences.Get(OfficeHoursKey, 5.0);
            HoursEntry.Text = savedHours.ToString("0.##");
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (double.TryParse(HoursEntry.Text, out double hours) && hours > 0)
            {
                Preferences.Set(OfficeHoursKey, hours);
                var snackbar = Snackbar.Make("Office hours goal updated successfully", duration: TimeSpan.FromSeconds(2));
                await snackbar.Show();
                WeakReferenceMessenger.Default.Send(new OfficeHoursUpdatedMessage(hours));
            }
            else
            {
                var snackbar = Snackbar.Make("Please enter a valid number of hours", duration: TimeSpan.FromSeconds(2));
                await snackbar.Show();
            }
        }

        private void OnDangerZoneToggled(object sender, EventArgs e)
        {
            bool isCurrentlyVisible = DangerZoneContent.IsVisible;
            DangerZoneContent.IsVisible = !isCurrentlyVisible;
            DangerZoneIcon.Text = isCurrentlyVisible ? "▶" : "▼";
        }

        private async void OnClearMonthClicked(object sender, EventArgs e)
        {
            var availableMonths = await GetAvailableMonths();

            if (availableMonths.Count == 0)
            {
                var snackbar = Snackbar.Make("No data found to clear", duration: TimeSpan.FromSeconds(2));
                await snackbar.Show();
                return;
            }

            PopulateMonthSelection(availableMonths);
            MonthSelectionOverlay.IsVisible = true;
        }

        private void PopulateMonthSelection(List<DateTime> availableMonths)
        {
            MonthsContainer.Children.Clear();
            selectedMonth = null;
            ConfirmClearButton.IsEnabled = false;
            ConfirmClearButton.BackgroundColor = Color.FromArgb("#FFE0B2");
            ConfirmClearButton.TextColor = Colors.Gray;

            foreach (var month in availableMonths.OrderByDescending(m => m))
            {
                var monthButton = new Border
                {
                    BackgroundColor = Colors.Transparent,
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#E0E0E0"),
                    Margin = new Thickness(0, 2),
                    HeightRequest = 56
                };

                monthButton.StrokeShape = new RoundRectangle { CornerRadius = 8 };

                var monthLabel = new Label
                {
                    Text = month.ToString("MMMM yyyy"),
                    FontSize = 16,
                    TextColor = Color.FromArgb("#333333"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                monthButton.Content = monthLabel;

                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += (s, e) => OnMonthSelected(month, monthButton, monthLabel);
                monthButton.GestureRecognizers.Add(tapGesture);

                MonthsContainer.Children.Add(monthButton);
            }
        }

        private void OnMonthSelected(DateTime month, Border button, Label label)
        {
            // Reset all buttons
            foreach (Border border in MonthsContainer.Children.OfType<Border>())
            {
                border.BackgroundColor = Colors.Transparent;
                border.Stroke = Color.FromArgb("#E0E0E0");
                if (border.Content is Label lbl)
                {
                    lbl.TextColor = Color.FromArgb("#333333");
                }
            }

            // Highlight selected button
            button.BackgroundColor = Color.FromArgb("#E3F2FD");
            button.Stroke = Color.FromArgb("#2196F3");
            label.TextColor = Color.FromArgb("#1976D2");

            selectedMonth = month;
            ConfirmClearButton.IsEnabled = true;
            ConfirmClearButton.BackgroundColor = Color.FromArgb("#FF8C00");
            ConfirmClearButton.TextColor = Colors.White;
        }

        private void OnCancelMonthSelection(object sender, EventArgs e)
        {
            MonthSelectionOverlay.IsVisible = false;
            selectedMonth = null;
        }

        private void OnConfirmClearMonth(object sender, EventArgs e)
        {
            if (!selectedMonth.HasValue) return;

            var monthName = selectedMonth.Value.ToString("MMMM yyyy");
            DeleteMonthMessage.Text = $"Are you sure you want to permanently delete all time tracking data for {monthName}?";

            MonthSelectionOverlay.IsVisible = false;
            DeleteMonthOverlay.IsVisible = true;
        }

        private void OnCancelDeleteMonth(object sender, EventArgs e)
        {
            DeleteMonthOverlay.IsVisible = false;
            MonthSelectionOverlay.IsVisible = true; // Show month selection again
        }

        private async void OnFinalConfirmDeleteMonth(object sender, EventArgs e)
        {
            if (!selectedMonth.HasValue) return;

            DeleteMonthOverlay.IsVisible = false;
            var monthName = selectedMonth.Value.ToString("MMMM yyyy");

            try
            {
                await ClearLogsForMonth(selectedMonth.Value.Year, selectedMonth.Value.Month);

                var snackbar = Snackbar.Make($"Data for {monthName} has been deleted", duration: TimeSpan.FromSeconds(3));
                await snackbar.Show();

                WeakReferenceMessenger.Default.Send(new LogsClearedMessage(selectedMonth.Value.Year, selectedMonth.Value.Month));
            }
            catch (Exception ex)
            {
                var snackbar = Snackbar.Make($"Error deleting data: {ex.Message}", duration: TimeSpan.FromSeconds(3));
                await snackbar.Show();
            }
        }

        private void OnClearAllClicked(object sender, EventArgs e)
        {
            DeleteAllOverlay.IsVisible = true;
        }

        private void OnCancelDeleteAll(object sender, EventArgs e)
        {
            DeleteAllOverlay.IsVisible = false;
        }

        private async void OnConfirmDeleteAll(object sender, EventArgs e)
        {
            DeleteAllOverlay.IsVisible = false;

            try
            {
                if (File.Exists(storagePath))
                {
                    File.Delete(storagePath);
                }

                var snackbar = Snackbar.Make("All data has been permanently deleted", duration: TimeSpan.FromSeconds(3));
                await snackbar.Show();

                WeakReferenceMessenger.Default.Send(new AllLogsClearedMessage());
            }
            catch (Exception ex)
            {
                var snackbar = Snackbar.Make($"Error deleting data: {ex.Message}", duration: TimeSpan.FromSeconds(3));
                await snackbar.Show();
            }
        }

        private async Task<List<DateTime>> GetAvailableMonths()
        {
            if (!File.Exists(storagePath))
                return new List<DateTime>();

            try
            {
                var json = await File.ReadAllTextAsync(storagePath);
                var allSwipes = JsonSerializer.Deserialize<List<DateTime>>(json) ?? new();

                var uniqueMonths = allSwipes
                    .GroupBy(date => new DateTime(date.Year, date.Month, 1))
                    .Select(g => g.Key)
                    .OrderByDescending(date => date)
                    .ToList();

                return uniqueMonths;
            }
            catch
            {
                return new List<DateTime>();
            }
        }

        private async Task ClearLogsForMonth(int year, int month)
        {
            if (!File.Exists(storagePath))
                return;

            var json = await File.ReadAllTextAsync(storagePath);
            var allSwipes = JsonSerializer.Deserialize<List<DateTime>>(json) ?? new();

            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var filteredSwipes = allSwipes.Where(swipe =>
                swipe < monthStart || swipe >= monthEnd
            ).ToList();

            var updatedJson = JsonSerializer.Serialize(filteredSwipes, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(storagePath, updatedJson);
        }
    }

    // Message classes
    public class LogsClearedMessage
    {
        public int Year { get; }
        public int Month { get; }

        public LogsClearedMessage(int year, int month)
        {
            Year = year;
            Month = month;
        }
    }

    public class AllLogsClearedMessage
    {
        public AllLogsClearedMessage() { }
    }
}