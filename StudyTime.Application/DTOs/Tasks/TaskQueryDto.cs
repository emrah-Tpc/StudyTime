using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyTime.Application.DTOs.Tasks
{
    public sealed class TaskQueryDto
    {
        public string? Status { get; set; }
        public Guid? LessonId { get; set; }
        public string? Search { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
