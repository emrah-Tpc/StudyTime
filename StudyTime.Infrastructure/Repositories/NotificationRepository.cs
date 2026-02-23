using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudyTime.Domain.Entities;
using StudyTime.Infrastructure.Persistence;

namespace StudyTime.Infrastructure.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly StudyTimeDbContext _context;

        public NotificationRepository(StudyTimeDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Notification>> GetAllAsync()
        {
            return await _context.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();
        }

        public async Task<Notification?> GetByIdAsync(Guid id)
        {
            return await _context.Notifications.FindAsync(id);
        }

        public async Task<Notification> AddAsync(Notification notification)
        {
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            return notification;
        }

        public async Task UpdateAsync(Notification notification)
        {
            _context.Entry(notification).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task MarkAllAsReadAsync()
        {
            var unread = await _context.Notifications.Where(n => !n.IsRead).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteOldNotificationsAsync(int daysToKeep)
        {
            var cutoff = DateTime.Now.AddDays(-daysToKeep);
            var old = await _context.Notifications.Where(n => n.CreatedAt < cutoff).ToListAsync();
            _context.Notifications.RemoveRange(old);
            await _context.SaveChangesAsync();
        }
    }
}
