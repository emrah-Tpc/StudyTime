using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StudyTime.Domain.Entities;

namespace StudyTime.Infrastructure.Repositories
{
    public interface INotificationRepository
    {
        Task<IEnumerable<Notification>> GetAllAsync();
        Task<Notification?> GetByIdAsync(Guid id);
        Task<Notification> AddAsync(Notification notification);
        Task UpdateAsync(Notification notification);
        Task MarkAllAsReadAsync();
        Task DeleteOldNotificationsAsync(int daysToKeep);
        Task DeleteAllAsync();
    }
}
