using System;

namespace StudyTime.DesktopClient.Interfaces
{
    public interface IPlatformNotificationHandler
    {
        void ShowOSNotification(string title, string message, Guid notificationId);
    }
}
