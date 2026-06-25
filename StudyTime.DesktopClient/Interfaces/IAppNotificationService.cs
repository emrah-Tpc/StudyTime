using System.Threading.Tasks;
using StudyTime.DesktopClient.Services; // NotificationCategory burada

namespace StudyTime.DesktopClient.Interfaces
{
    public interface IAppNotificationService
    {
        Task SendNotificationAsync(NotificationCategory category, string title, string message);
        bool IsAppInForeground();
    }
}
