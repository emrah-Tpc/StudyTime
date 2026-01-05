using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.Application.Services;

namespace StudyTime.Controllers
{
    [ApiController]
    [Route("api/tasks")]
    public class TaskController(TaskService taskService) : ControllerBase
    {
        // CREATE
        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto dto)
        {
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

        // COMPLETE
        [HttpPost("{id:guid}/complete")]
        public async Task<IActionResult> Complete(Guid id)
        {
            try
            {
                await taskService.CompleteTaskAsync(id);
                return NoContent();
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // CANCEL
        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            try
            {
                await taskService.CancelTaskAsync(id);
                return NoContent();
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // DELETE (Artık Soft Delete çalışacak)
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await taskService.DeleteTaskAsync(id);
                return NoContent();
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // REOPEN
        [HttpPost("{id:guid}/reopen")]
        public async Task<IActionResult> Reopen(Guid id)
        {
            try
            {
                await taskService.ReopenTaskAsync(id);
                return NoContent();
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // UPDATE
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskDto dto)
        {
            try
            {
                await taskService.UpdateTaskAsync(id, dto);
                return NoContent();
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }
    }
}