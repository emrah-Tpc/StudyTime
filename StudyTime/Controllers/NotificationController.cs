using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.DTOs.Notifications;
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
        public async Task<ActionResult<IEnumerable<NotificationDto>>> GetNotifications()
        {
            var notifications = await _repository.GetAllAsync();
            // F17: Domain entity (UserId/IsDeleted gibi iç alanlar) sızdırma — DTO'ya projeksiyon.
            return Ok(notifications.Select(ToDto));
        }

        [HttpPost]
        public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationDto dto)
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { message = "Başlık zorunludur." });

            // Güvenli kurulum: UserId/IsRead/IsDeleted ASLA istemciden alınmaz.
            // UserId, DbContext.ApplyUserIdToEntities tarafından geçerli kullanıcıya atanır.
            var notification = new Notification
            {
                Id        = dto.Id.HasValue && dto.Id.Value != Guid.Empty ? dto.Id.Value : Guid.NewGuid(),
                Title     = dto.Title,
                Message   = dto.Message ?? string.Empty,
                Category  = string.IsNullOrWhiteSpace(dto.Category) ? "System" : dto.Category!,
                ActionUrl = dto.ActionUrl,
                CreatedAt = dto.CreatedAt ?? DateTime.UtcNow,
                IsRead    = false,
                IsDeleted = false
            };

            var created = await _repository.AddAsync(notification);
            return Ok(ToDto(created));
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

        // F46: İstemci 'DELETE api/notification/all' çağırıyordu; endpoint eksikti (404).
        [HttpDelete("all")]
        public async Task<IActionResult> DeleteAll()
        {
            await _repository.DeleteAllAsync();
            return NoContent();
        }

        private static NotificationDto ToDto(Notification n) => new()
        {
            Id        = n.Id,
            Title     = n.Title,
            Message   = n.Message,
            Category  = n.Category,
            IsRead    = n.IsRead,
            ActionUrl = n.ActionUrl,
            CreatedAt = n.CreatedAt
        };
    }
}
