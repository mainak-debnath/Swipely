using Microsoft.Maui.Controls;

namespace TimeTracker
{
    public partial class AppShell : Shell
    {
        private bool hasAnimatedHeader = false;

        public AppShell()
        {
            InitializeComponent();
            this.FlyoutIsPresented = false;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (!hasAnimatedHeader)
            {
                AnimateHeaderElements();
                hasAnimatedHeader = true;
            }
        }

        private async void AnimateHeaderElements()
        {
            try
            {
                var appIcon = this.FindByName<Frame>("AppIcon");
                var appTitle = this.FindByName<Label>("AppTitle");
                var appSubtitle = this.FindByName<Label>("AppSubtitle");

                if (appIcon != null && appTitle != null && appSubtitle != null)
                {
                    appIcon.Scale = 0;
                    appTitle.Opacity = 0;
                    appTitle.TranslationX = -100;
                    appSubtitle.Opacity = 0;
                    appSubtitle.TranslationX = -50;

                    var iconTask = appIcon.ScaleTo(1.0, 800, Easing.BounceOut);

                    var titleTask1 = appTitle.TranslateTo(0, 0, 700, Easing.CubicOut);
                    var titleTask2 = appTitle.FadeTo(1.0, 700);

                    await Task.Delay(300);
                    var subtitleTask1 = appSubtitle.TranslateTo(0, 0, 600, Easing.CubicOut);
                    var subtitleTask2 = appSubtitle.FadeTo(0.8, 600);

                    await Task.WhenAll(iconTask, titleTask1, titleTask2, subtitleTask1, subtitleTask2);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Header animation error: {ex.Message}");
            }
        }

        protected override void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            if (propertyName == nameof(FlyoutIsPresented))
            {
                if (FlyoutIsPresented)
                {
                    AnimateMenuItems();
                }
            }
        }

        private async void AnimateMenuItems()
        {
            await Task.Delay(50);
        }
    }
}