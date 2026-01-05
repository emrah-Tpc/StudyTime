using StudyTime.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyTime.Application.DTOs.Lessons
{
    public sealed class CreateLessonDto
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public LessonType Type { get; set; } = LessonType.Academic;
    }
}
