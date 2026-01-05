using System;

namespace StudyTime.Domain.Entities
{
    public class StudySession
    {
        public Guid Id { get; private set; }
        public Guid LessonId { get; private set; }
        public Guid? TaskId { get; private set; } // private set olduğu için hata veriyordu

        public DateTime StartedAt { get; private set; }
        public DateTime? EndedAt { get; private set; }
        public DateTime? LastResumedAt { get; private set; }
        public TimeSpan TotalActiveDuration { get; private set; }

        // 👇 CONSTRUCTOR GÜNCELLENDİ: TaskId parametresi eklendi
        public StudySession(Guid lessonId, Guid? taskId = null)
        {
            Id = Guid.NewGuid();
            LessonId = lessonId;
            TaskId = taskId; // İlk oluşum anında değeri atıyoruz

            StartedAt = DateTime.MinValue;
            TotalActiveDuration = TimeSpan.Zero;
            LastResumedAt = null;
        }

        public void Start()
        {
            StartedAt = DateTime.UtcNow;
            LastResumedAt = DateTime.UtcNow;
            EndedAt = null;
        }

        public void Stop()
        {
            if (EndedAt != null) return;

            if (LastResumedAt.HasValue)
            {
                TotalActiveDuration += DateTime.UtcNow - LastResumedAt.Value;
            }

            EndedAt = DateTime.UtcNow;
            LastResumedAt = null;
        }

        public void Pause()
        {
            if (LastResumedAt.HasValue)
            {
                TotalActiveDuration += DateTime.UtcNow - LastResumedAt.Value;
                LastResumedAt = null;
            }
        }

        public void Resume()
        {
            if (EndedAt == null && LastResumedAt == null)
            {
                LastResumedAt = DateTime.UtcNow;
            }
        }

        public TimeSpan CurrentDuration
        {
            get
            {
                if (EndedAt.HasValue) return TotalActiveDuration;
                if (LastResumedAt.HasValue)
                    return TotalActiveDuration + (DateTime.UtcNow - LastResumedAt.Value);
                return TotalActiveDuration;
            }
        }

        public bool IsActive => EndedAt == null && StartedAt != DateTime.MinValue;
    }
}