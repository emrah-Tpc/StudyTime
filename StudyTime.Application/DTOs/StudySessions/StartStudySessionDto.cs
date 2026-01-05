using System;

namespace StudyTime.Application.DTOs.StudySessions
{
    public class StartStudySessionDto
    {
        public Guid LessonId { get; set; }
        public Guid? TaskId { get; set; }
    }
}