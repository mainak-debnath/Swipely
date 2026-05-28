using CommunityToolkit.Maui;
#if ANDROID
using Android.Graphics.Drawables;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
#endif

namespace TimeTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
#if ANDROID
                .ConfigureMauiHandlers(handlers =>
                {
                    EntryHandler.Mapper.AppendToMapping("CursorColor", (handler, _) =>
                    {
                        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                        {
                            return;
                        }

                        var cursorColor = Application.Current?.Resources["Accent"] as Color ?? Colors.White;
                        var cursorDrawable = new GradientDrawable();
                        cursorDrawable.SetColor(cursorColor.ToPlatform());
                        cursorDrawable.SetSize(3, (int)(handler.PlatformView.TextSize * 1.4));
                        handler.PlatformView.TextCursorDrawable = cursorDrawable;
                    });
                })
#endif
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            return builder.Build();
        }
    }
}
