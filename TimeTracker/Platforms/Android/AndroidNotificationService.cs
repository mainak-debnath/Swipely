#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace TimeTracker.Helpers;

public static partial class NotificationService
{
    private const string ChannelId = "swipely_time_tracking";

    private static partial async Task<bool> ShowAsync(int notificationId, string title, string message)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            }

            if (status != PermissionStatus.Granted)
            {
                return false;
            }
        }

        var context = Platform.AppContext;
        var notificationManager = NotificationManagerCompat.From(context);
        EnsureChannel(context);

        var packageName = context.PackageName ?? string.Empty;
        var intent = context.PackageManager?.GetLaunchIntentForPackage(packageName);
        intent?.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        var pendingIntentFlags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            pendingIntentFlags |= PendingIntentFlags.Immutable;
        }

        var pendingIntent = PendingIntent.GetActivity(
            context,
            0,
            intent,
            pendingIntentFlags);

        var notification = new NotificationCompat.Builder(context, ChannelId)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .SetPriority(NotificationCompat.PriorityDefault)
            .Build();

        notificationManager.Notify(notificationId, notification);
        return true;
    }

    private static void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var channel = new NotificationChannel(
            ChannelId,
            "Swipely reminders",
            NotificationImportance.Default)
        {
            Description = "Office goal and swipe reminder notifications"
        };

        var notificationManager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        notificationManager?.CreateNotificationChannel(channel);
    }
}
#endif
