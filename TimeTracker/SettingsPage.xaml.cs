using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls.Shapes;
using System.Text.Json;
using TimeTracker.Helpers;

namespace TimeTracker;

public partial class SettingsPage : ContentPage
{
    private string storagePath => TimeTrackingService.StoragePath;
    private DateTime? selectedMonth;
    private bool suppressNotificationSelection;
    private bool isSaveAnimating;

    public SettingsPage()
    {
        InitializeComponent();
        LoadCurrentValues();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadCurrentValues();
    }

    private void LoadCurrentValues()
    {
        HoursEntry.Text = TimeTrackingService.GetOfficeHoursGoal().ToString("0.##");
        OfficeDaysEntry.Text = TimeTrackingService.GetOfficeDaysPerWeekGoal().ToString();

        UpdateThemeSelector(AppThemeManager.GetSavedTheme());

        suppressNotificationSelection = true;
        GoalNotificationSwitch.IsToggled = NotificationPreferences.GoalNotificationsEnabled;
        LongSessionNotificationSwitch.IsToggled = NotificationPreferences.LongSessionNotificationsEnabled;
        LongSessionHoursEntry.Text = NotificationPreferences.LongSessionReminderHours.ToString("0.##");
        UpdateLongSessionReminderSettings(LongSessionNotificationSwitch.IsToggled);
        suppressNotificationSelection = false;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        SaveGoalButton.IsEnabled = false;

        bool hasValidHours = double.TryParse(HoursEntry.Text, out double hours) && hours > 0;
        bool hasValidOfficeDays = int.TryParse(OfficeDaysEntry.Text, out int officeDays) && officeDays is >= 1 and <= 5;

        if (hasValidHours && hasValidOfficeDays)
        {
            TimeTrackingService.SetOfficeHoursGoal(hours);
            TimeTrackingService.SetOfficeDaysPerWeekGoal(officeDays);
            await PlaySaveSuccessAnimationAsync();
            await Snackbar.Make("Goals updated successfully", duration: TimeSpan.FromSeconds(2)).Show();
            WeakReferenceMessenger.Default.Send(new OfficeHoursUpdatedMessage(hours));
        }
        else
        {
            await PlaySaveErrorAnimationAsync();
            await Snackbar.Make("Enter valid hours and 1-5 office days", duration: TimeSpan.FromSeconds(2)).Show();
        }

        SaveGoalButton.IsEnabled = true;
    }

    private async void ThemeSelector_Tapped(object sender, TappedEventArgs e)
    {
        UpdateThemeSelector(AppThemeManager.GetSavedTheme());

        ThemeSelectionOverlay.IsVisible = true;
        ThemeSelectionOverlay.Opacity = 0;
        ThemeSelectionCard.Scale = 0.94;
        ThemeSelectionCard.TranslationY = 18;

        await Task.WhenAll(
            ThemeSelectionOverlay.FadeTo(1, 180, Easing.CubicOut),
            ThemeSelectionCard.ScaleTo(1, 260, Easing.CubicOut),
            ThemeSelectionCard.TranslateTo(0, 0, 260, Easing.CubicOut));
    }

    private async void ThemeAuto_Tapped(object sender, TappedEventArgs e) => await SelectThemeAsync("Auto");

    private async void ThemeLight_Tapped(object sender, TappedEventArgs e) => await SelectThemeAsync("Light");

    private async void ThemeDark_Tapped(object sender, TappedEventArgs e) => await SelectThemeAsync("Dark");

    private async void ThemeSelectionClose_Clicked(object sender, EventArgs e) => await CloseThemeSelectionAsync();

    private async void ThemeSelectionBackdrop_Tapped(object sender, TappedEventArgs e) => await CloseThemeSelectionAsync();

    private void ThemeSelectionCard_Tapped(object sender, TappedEventArgs e)
    {
        // This prevents a tap inside the card from behaving like a backdrop dismiss.
    }

    private async Task SelectThemeAsync(string themeName)
    {
        if (Application.Current is null)
        {
            return;
        }

        AppThemeManager.SaveAndApplyTheme(Application.Current, themeName);
        UpdateThemeSelector(themeName);
        await CloseThemeSelectionAsync();
        await Snackbar.Make($"Theme set to {themeName}", duration: TimeSpan.FromSeconds(2)).Show();
    }

    private async Task CloseThemeSelectionAsync()
    {
        if (!ThemeSelectionOverlay.IsVisible)
        {
            return;
        }

        await Task.WhenAll(
            ThemeSelectionOverlay.FadeTo(0, 150, Easing.CubicIn),
            ThemeSelectionCard.ScaleTo(0.96, 150, Easing.CubicIn),
            ThemeSelectionCard.TranslateTo(0, 12, 150, Easing.CubicIn));

        ThemeSelectionOverlay.IsVisible = false;
        ThemeSelectionCard.Scale = 1;
        ThemeSelectionCard.TranslationY = 0;
    }

    private void UpdateThemeSelector(string themeName)
    {
        ThemeSelectorLabel.Text = themeName;
        ThemeSelectorIcon.Text = themeName[..1];

        UpdateThemeOption(ThemeAutoOption, ThemeAutoCheck, themeName == "Auto");
        UpdateThemeOption(ThemeLightOption, ThemeLightCheck, themeName == "Light");
        UpdateThemeOption(ThemeDarkOption, ThemeDarkCheck, themeName == "Dark");
    }

    private static void UpdateThemeOption(Border option, Label checkLabel, bool isSelected)
    {
        option.BackgroundColor = isSelected
            ? ThemeColor("AccentSoftLight", "AccentSoftDark")
            : ThemeColor("SurfaceAltLight", "SurfaceAltDark");
        option.Stroke = isSelected
            ? ThemeColor("Accent", "Accent")
            : ThemeColor("BorderColorLight", "BorderColorDark");
        option.StrokeThickness = isSelected ? 2 : 1;
        checkLabel.Text = isSelected ? "Selected" : string.Empty;
    }

    private async void GoalNotificationSwitch_Toggled(object sender, ToggledEventArgs e)
    {
        if (suppressNotificationSelection)
        {
            return;
        }

        NotificationPreferences.GoalNotificationsEnabled = e.Value;
        await Snackbar.Make(e.Value ? "Goal notifications enabled" : "Goal notifications disabled", duration: TimeSpan.FromSeconds(2)).Show();
    }

    private async void LongSessionNotificationSwitch_Toggled(object sender, ToggledEventArgs e)
    {
        if (suppressNotificationSelection)
        {
            return;
        }

        NotificationPreferences.LongSessionNotificationsEnabled = e.Value;
        UpdateLongSessionReminderSettings(e.Value);
        await Snackbar.Make(e.Value ? "Long-session reminders enabled" : "Long-session reminders disabled", duration: TimeSpan.FromSeconds(2)).Show();
    }

    private async void LongSessionHoursEntry_Unfocused(object sender, FocusEventArgs e)
    {
        if (!LongSessionNotificationSwitch.IsToggled)
        {
            LongSessionHoursEntry.Text = NotificationPreferences.LongSessionReminderHours.ToString("0.##");
            return;
        }

        if (double.TryParse(LongSessionHoursEntry.Text, out double hours) && hours >= 1)
        {
            NotificationPreferences.LongSessionReminderHours = hours;
            LongSessionHoursEntry.Text = NotificationPreferences.LongSessionReminderHours.ToString("0.##");
            return;
        }

        LongSessionHoursEntry.Text = NotificationPreferences.LongSessionReminderHours.ToString("0.##");
        await Snackbar.Make("Enter at least 1 hour for the reminder", duration: TimeSpan.FromSeconds(2)).Show();
    }

    private void UpdateLongSessionReminderSettings(bool isEnabled)
    {
        LongSessionReminderSettings.IsEnabled = isEnabled;
        LongSessionHoursEntry.IsEnabled = isEnabled;
        LongSessionReminderSettings.Opacity = isEnabled ? 1 : 0.48;
    }

    private void OnDangerZoneToggled(object sender, EventArgs e)
    {
        bool isCurrentlyVisible = DangerZoneContent.IsVisible;
        DangerZoneContent.IsVisible = !isCurrentlyVisible;
        DangerZoneIcon.Rotation = isCurrentlyVisible ? 90 : 0;
    }

    private async void OnClearMonthClicked(object sender, EventArgs e)
    {
        var availableMonths = await GetAvailableMonths();

        if (availableMonths.Count == 0)
        {
            await Snackbar.Make("No data found to clear", duration: TimeSpan.FromSeconds(2)).Show();
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
        ConfirmClearButton.BackgroundColor = Color.FromArgb("#E7EEF8");
        ConfirmClearButton.TextColor = Color.FromArgb("#60708A");

        foreach (var month in availableMonths.OrderByDescending(m => m))
        {
            var monthButton = new Border
            {
                BackgroundColor = Colors.Transparent,
                StrokeThickness = 1,
                Stroke = ThemeColor("BorderColorLight", "BorderColorDark"),
                Margin = new Thickness(0, 2),
                HeightRequest = 56
            };

            monthButton.StrokeShape = new RoundRectangle { CornerRadius = 14 };

            var monthLabel = new Label
            {
                Text = month.ToString("MMMM yyyy"),
                FontSize = 16,
                TextColor = ThemeColor("TextPrimaryLight", "TextPrimaryDark"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            monthButton.Content = monthLabel;

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (_, _) => OnMonthSelected(month, monthButton, monthLabel);
            monthButton.GestureRecognizers.Add(tapGesture);

            MonthsContainer.Children.Add(monthButton);
        }
    }

    private void OnMonthSelected(DateTime month, Border button, Label label)
    {
        foreach (Border border in MonthsContainer.Children.OfType<Border>())
        {
            border.BackgroundColor = Colors.Transparent;
            border.Stroke = ThemeColor("BorderColorLight", "BorderColorDark");

            if (border.Content is Label lbl)
            {
                lbl.TextColor = ThemeColor("TextPrimaryLight", "TextPrimaryDark");
            }
        }

        button.BackgroundColor = ThemeColor("AccentSoftLight", "AccentSoftDark");
        button.Stroke = Color.FromArgb("#2D5BFF");
        label.TextColor = Color.FromArgb("#2D5BFF");

        selectedMonth = month;
        ConfirmClearButton.IsEnabled = true;
        ConfirmClearButton.BackgroundColor = Color.FromArgb("#D97706");
        ConfirmClearButton.TextColor = Colors.White;
    }

    private void OnCancelMonthSelection(object sender, EventArgs e)
    {
        MonthSelectionOverlay.IsVisible = false;
        selectedMonth = null;
    }

    private void OnConfirmClearMonth(object sender, EventArgs e)
    {
        if (!selectedMonth.HasValue)
        {
            return;
        }

        DeleteMonthMessage.Text = $"Are you sure you want to permanently delete all time tracking data for {selectedMonth.Value:MMMM yyyy}?";
        MonthSelectionOverlay.IsVisible = false;
        DeleteMonthOverlay.IsVisible = true;
    }

    private void OnCancelDeleteMonth(object sender, EventArgs e)
    {
        DeleteMonthOverlay.IsVisible = false;
        MonthSelectionOverlay.IsVisible = true;
    }

    private async void OnFinalConfirmDeleteMonth(object sender, EventArgs e)
    {
        if (!selectedMonth.HasValue)
        {
            return;
        }

        DeleteMonthOverlay.IsVisible = false;
        string monthName = selectedMonth.Value.ToString("MMMM yyyy");

        try
        {
            await ClearLogsForMonth(selectedMonth.Value.Year, selectedMonth.Value.Month);
            await Snackbar.Make($"Data for {monthName} has been deleted", duration: TimeSpan.FromSeconds(3)).Show();
            WeakReferenceMessenger.Default.Send(new LogsClearedMessage(selectedMonth.Value.Year, selectedMonth.Value.Month));
        }
        catch (Exception ex)
        {
            await Snackbar.Make($"Error deleting data: {ex.Message}", duration: TimeSpan.FromSeconds(3)).Show();
        }
    }

    private void OnClearAllClicked(object sender, EventArgs e) => DeleteAllOverlay.IsVisible = true;

    private void OnCancelDeleteAll(object sender, EventArgs e) => DeleteAllOverlay.IsVisible = false;

    private async void OnConfirmDeleteAll(object sender, EventArgs e)
    {
        DeleteAllOverlay.IsVisible = false;

        try
        {
            if (File.Exists(storagePath))
            {
                File.Delete(storagePath);
            }

            await Snackbar.Make("All data has been permanently deleted", duration: TimeSpan.FromSeconds(3)).Show();
            WeakReferenceMessenger.Default.Send(new AllLogsClearedMessage());
        }
        catch (Exception ex)
        {
            await Snackbar.Make($"Error deleting data: {ex.Message}", duration: TimeSpan.FromSeconds(3)).Show();
        }
    }

    private async Task<List<DateTime>> GetAvailableMonths()
    {
        if (!File.Exists(storagePath))
        {
            return new List<DateTime>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(storagePath);
            var allSwipes = JsonSerializer.Deserialize<List<DateTime>>(json) ?? new List<DateTime>();

            return allSwipes
                .GroupBy(date => new DateTime(date.Year, date.Month, 1))
                .Select(group => group.Key)
                .OrderByDescending(date => date)
                .ToList();
        }
        catch
        {
            return new List<DateTime>();
        }
    }

    private async Task ClearLogsForMonth(int year, int month)
    {
        if (!File.Exists(storagePath))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(storagePath);
        var allSwipes = JsonSerializer.Deserialize<List<DateTime>>(json) ?? new List<DateTime>();

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var filteredSwipes = allSwipes
            .Where(swipe => swipe < monthStart || swipe >= monthEnd)
            .ToList();

        var updatedJson = JsonSerializer.Serialize(filteredSwipes, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(storagePath, updatedJson);
    }

    private static Color ThemeColor(string lightKey, string darkKey)
    {
        bool isDark = Application.Current?.RequestedTheme == AppTheme.Dark || Application.Current?.UserAppTheme == AppTheme.Dark;
        string key = isDark ? darkKey : lightKey;
        return Application.Current?.Resources[key] as Color ?? Colors.Transparent;
    }

    private async void SaveGoalButton_Pressed(object sender, EventArgs e)
    {
        if (isSaveAnimating)
        {
            return;
        }

        await SaveGoalButton.ScaleTo(0.97, 70, Easing.CubicOut);
    }

    private async void SaveGoalButton_Released(object sender, EventArgs e)
    {
        if (isSaveAnimating)
        {
            return;
        }

        await SaveGoalButton.ScaleTo(1.0, 110, Easing.CubicOut);
    }

    private async Task PlaySaveSuccessAnimationAsync()
    {
        isSaveAnimating = true;

        string originalText = SaveGoalButton.Text;
        Color originalBackground = SaveGoalButton.BackgroundColor;
        Color originalTextColor = SaveGoalButton.TextColor;

        SaveGoalButton.Text = "Saved";
        SaveGoalButton.TextColor = Colors.White;
        SaveGoalButton.BackgroundColor = ThemeColor("Success", "Success");

        await SaveGoalButton.ScaleTo(1.0, 90, Easing.CubicOut);
        await SaveGoalButton.FadeTo(0.88, 90, Easing.CubicInOut);
        await SaveGoalButton.FadeTo(1.0, 120, Easing.CubicInOut);
        await Task.Delay(220);

        SaveGoalButton.Text = originalText;
        SaveGoalButton.BackgroundColor = originalBackground;
        SaveGoalButton.TextColor = originalTextColor;
        isSaveAnimating = false;
    }

    private async Task PlaySaveErrorAnimationAsync()
    {
        isSaveAnimating = true;

        await SaveGoalButton.ScaleTo(1.0, 70, Easing.CubicOut);
        await SaveGoalButton.TranslateTo(-6, 0, 45, Easing.CubicInOut);
        await SaveGoalButton.TranslateTo(6, 0, 45, Easing.CubicInOut);
        await SaveGoalButton.TranslateTo(0, 0, 45, Easing.CubicInOut);

        isSaveAnimating = false;
    }
}

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
}
