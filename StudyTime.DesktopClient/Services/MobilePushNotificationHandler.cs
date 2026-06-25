#if ANDROID || IOS || MACCATALYST
using System;
using System.Diagnostics;
using StudyTime.DesktopClient.Interfaces;

namespace StudyTime.DesktopClient.Services
{
    public class MobilePushNotificationHandler : IPlatformNotificationHandler
    {
        public void ShowOSNotification(string title, string message, Guid notificationId)
        {
            // TODO: Plugin.LocalNotification kurulduğunda burayı doldurun.
            // Örnek kullanım:
            // var request = new NotificationRequest
            // {
            //     NotificationId = notificationId.GetHashCode(),
            //     Title = title,
            //     Description = message,
            //     CategoryType = NotificationCategoryType.Event
            // };
            // LocalNotificationCenter.Current.Show(request);

            Debug.WriteLine($"[MOBILE PUSH STUB] {title}: {message}");
        }
    }
}
#endif
