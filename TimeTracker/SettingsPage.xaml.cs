using Microsoft.Maui.Storage;

namespace TimeTracker
{
    public partial class SettingsPage : ContentPage
    {
        const string OfficeHoursKey = "OfficeHoursGoal";

        public SettingsPage()
        {
            InitializeComponent();

            // Load previously saved value
            double savedHours = Preferences.Get(OfficeHoursKey, 5.0); // default = 5
            HoursEntry.Text = savedHours.ToString("0.##");
        }

        private void OnSaveClicked(object sender, EventArgs e)
        {
            if (double.TryParse(HoursEntry.Text, out double hours) && hours > 0)
            {
                Preferences.Set(OfficeHoursKey, hours);
                DisplayAlert("Success", "Office hours goal saved!", "OK");
            }
            else
            {
                DisplayAlert("Invalid", "Please enter a valid number of hours.", "OK");
            }
        }
    }
}
