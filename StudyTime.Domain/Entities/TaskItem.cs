using System;
using StudyTime.Domain.Enums;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Domain.Entities
{
    public class TaskItem
    {
        public Guid Id { get; private set; }
        public Guid? LessonId { get; private set; }
        public Lesson? Lesson { get; private set; }

        public string Title { get; private set; } = null!;
        public string? Note { get; private set; }
        public DateTime? StartDate { get; private set; }
        public DateTime? EndDate { get; private set; }
        public TimeSpan? PlannedDuration { get; private set; }

        // 🔴 BURASI ÖNEMLİ
        public TaskStatus Status { get; private set; }

        public bool IsDeleted { get; private set; }

        private TaskItem() { } // EF Core için

        public TaskItem(
            string title,
            Guid? lessonId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? note = null,
            TimeSpan? plannedDuration = null)
        {
            Id = Guid.NewGuid();
            ChangeTitle(title);
            AssignLesson(lessonId);
            UpdateDates(startDate, endDate);
            UpdateNote(note);
            UpdatePlannedDuration(plannedDuration);
            Status = TaskStatus.Pending;
            IsDeleted = false;
        }

        public void ChangeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title cannot be empty.");
            Title = title;
        }

        public void UpdateNote(string? note)
        {
            Note = note;
        }

        public void UpdateDates(DateTime? start, DateTime? end)
        {
            if (start.HasValue && end.HasValue && end < start)
                throw new InvalidOperationException("End date cannot be before start date.");
            StartDate = start;
            EndDate = end;
        }

        public void UpdatePlannedDuration(TimeSpan? duration)
        {
            if (duration.HasValue && duration.Value <= TimeSpan.Zero)
                throw new InvalidOperationException("Duration must be positive.");
            PlannedDuration = duration;
        }

        public void AssignLesson(Guid? lessonId)
        {
            if (Status == TaskStatus.Completed)
                throw new InvalidOperationException("Completed task cannot change lesson.");
            LessonId = lessonId;
        }

        public void Complete()
        {
            if (Status != TaskStatus.Pending)
                throw new InvalidOperationException("Only pending tasks can be completed.");
            Status = TaskStatus.Completed;
        }

        public void Cancel()
        {
            if (Status == TaskStatus.Completed)
                throw new InvalidOperationException("Completed task cannot be cancelled.");
            Status = TaskStatus.Cancelled;
        }

        public void Reopen()
        {
            if (Status != TaskStatus.Cancelled)
                throw new InvalidOperationException("Only cancelled tasks can be reopened.");
            Status = TaskStatus.Pending;
        }

        public void Delete()
        {
            if (IsDeleted)
                throw new InvalidOperationException("Task already deleted.");
            IsDeleted = true;
        }
    }
}
