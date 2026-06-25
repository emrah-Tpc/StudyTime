using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.Application.Services;

namespace StudyTime.Controllers
{
    [ApiController]
    [Route("api/tasks")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class TaskController(TaskService taskService) : ControllerBase
    {
        // CREATE
        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                Console.WriteLine($"[API VALIDATION ERROR] {errors}");
                return BadRequest(errors);
            }

            if (dto == null) return BadRequest("Veri boş geldi.");

            if (dto.PlannedDurationMinutes.HasValue && dto.PlannedDurationMinutes.Value > 1440)
            {
                return BadRequest("Planlanan süre 24 saati (1440 dakika) geçemez.");
            }

            TimeSpan? duration = dto.PlannedDurationMinutes.HasValue
                ? TimeSpan.FromMinutes(dto.PlannedDurationMinutes.Value)
                : null;

            var taskId = await taskService.CreateTaskAsync(
                dto.Title,
                dto.LessonId,
                dto.StartDate,
                dto.EndDate,
                dto.Note,
                duration
            );

            return Ok(new { TaskId = taskId });
        }

        // GET ALL
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var tasks = await taskService.GetAllTasksAsync();
            return Ok(tasks);
        }

        // GET BY ID
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var task = await taskService.GetTaskByIdAsync(id);
            if (task is null) return NotFound();
            return Ok(task);
        }

        // COMPLETE  (hata eşleme: GlobalExceptionHandler — DataConflict→409, InvalidOperation→400)
        [HttpPost("{id:guid}/complete")]
        public async Task<IActionResult> Complete(Guid id, [FromQuery] DateTime? updatedAt = null)
        {
            await taskService.CompleteTaskAsync(id, updatedAt);
            return NoContent();
        }

        // CANCEL
        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id, [FromQuery] DateTime? updatedAt = null)
        {
            await taskService.CancelTaskAsync(id, updatedAt);
            return NoContent();
        }

        // DELETE (Soft Delete)
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, [FromQuery] DateTime? updatedAt = null)
        {
            await taskService.DeleteTaskAsync(id, updatedAt);
            return NoContent();
        }

        // REOPEN
        [HttpPost("{id:guid}/reopen")]
        public async Task<IActionResult> Reopen(Guid id, [FromQuery] DateTime? updatedAt = null)
        {
            await taskService.ReopenTaskAsync(id, updatedAt);
            return NoContent();
        }

        // UPDATE
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskDto dto)
        {
            await taskService.UpdateTaskAsync(id, dto);
            return NoContent();
        }
        // GET BY DATE RANGE
        [HttpGet("range")]
        public async Task<IActionResult> GetByDateRange([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            var tasks = await taskService.GetTasksByDateRangeAsync(start, end);
            return Ok(tasks);
        }
    }
}