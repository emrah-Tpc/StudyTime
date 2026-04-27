using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StudyTime.Domain.Entities;
using StudyTime.Infrastructure.Repositories;

namespace StudyTime.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationRepository _repository;

        public NotificationController(INotificationRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Notification>>> GetNotifications()
        {
            var notifications = await _repository.GetAllAsync();
            return Ok(notifications);
        }

        [HttpPost]
        public async Task<ActionResult<Notification>> CreateNotification(Notification notification)
        {
            notification.Id = Guid.NewGuid();
            notification.CreatedAt = DateTime.Now;
            notification.IsRead = false;
            
            var created = await _repository.AddAsync(notification);
            return CreatedAtAction(nameof(GetNotifications), new { id = created.Id }, created);
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id, [FromQuery] DateTime? updatedAt = null)
        {
            var notification = await _repository.GetByIdAsync(id);
            if (notification == null) return NotFound();

            if (updatedAt.HasValue && notification.UpdatedAt.HasValue && updatedAt < notification.UpdatedAt) return NoContent();

            notification.IsRead = true;
            notification.UpdatedAt = updatedAt ?? DateTime.UtcNow;
            await _repository.UpdateAsync(notification);
            return NoContent();
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await _repository.MarkAllAsReadAsync();
            return NoContent();
        }

        [HttpDelete("cleanup")]
        public async Task<IActionResult> Cleanup(int days = 30)
        {
            await _repository.DeleteOldNotificationsAsync(days);
            return NoContent();
        }
    }
}
