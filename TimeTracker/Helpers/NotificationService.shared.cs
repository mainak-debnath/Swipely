#if !ANDROID
namespace TimeTracker.Helpers;

public static partial class NotificationService
{
    private static partial Task<bool> ShowAsync(int notificationId, string title, string message) => Task.FromResult(false);
}
#endif
