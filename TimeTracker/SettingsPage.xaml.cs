using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.Messaging;
using TimeTracker.Helpers;

namespace TimeTracker
{
    public partial class SettingsPage : ContentPage
    {
        const string OfficeHoursKey = "OfficeHoursGoal";

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

                var snackbar = Snackbar.Make("Office hours goal saved!", duration: TimeSpan.FromSeconds(2));
                await snackbar.Show();

                WeakReferenceMessenger.Default.Send(new OfficeHoursUpdatedMessage(hours)); // Notify other pages
            }
            else
            {
                var snackbar = Snackbar.Make("Please enter a valid number of hours.", duration: TimeSpan.FromSeconds(2));
                await snackbar.Show();
            }
        }
    }
}