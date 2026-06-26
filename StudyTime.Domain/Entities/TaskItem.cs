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
        public DateTime? UpdatedAt { get; set; }
        public DateTime CreatedAt { get; private set; }

        // Auth Properties
        public string? UserId { get; set; }
        public AppUser? User { get; set; }

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
            CreatedAt = DateTime.UtcNow;
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

        // Durum geçişleri IDEMPOTENT'tir: zaten hedef durumdaysa no-op (exception fırlatmaz).
        // Böylece istemci toggle'ı exception'ı akış kontrolü olarak kullanmaz ve sunucu
        // normal işlemler için sahte hata logu üretmez. Yalnız GERÇEKTEN geçersiz geçişler fırlatır.
        public void Complete()
        {
            if (Status == TaskStatus.Completed) return;          // idempotent
            if (Status == TaskStatus.Cancelled)
                throw new InvalidOperationException("Cancelled task cannot be completed.");
            Status = TaskStatus.Completed;
        }

        public void Cancel()
        {
            if (Status == TaskStatus.Cancelled) return;          // idempotent
            if (Status == TaskStatus.Completed)
                throw new InvalidOperationException("Completed task cannot be cancelled.");
            Status = TaskStatus.Cancelled;
        }

        public void Reopen()
        {
            // İptal edilmiş VEYA tamamlanmış görev yeniden Pending'e alınabilir (F05).
            if (Status == TaskStatus.Pending) return;            // idempotent
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
