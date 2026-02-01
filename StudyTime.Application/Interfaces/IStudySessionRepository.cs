using StudyTime.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyTime.Application.Interfaces
{
    //Interface says that What to does?. It like agreement.
    public interface IStudySessionRepository
    {
        Task AddAsync(StudySession studySession);
        Task<StudySession?> GetActiveByLessonIdAsync(Guid lessonId);
        Task UpdateAsync(StudySession studySession);
        Task<List<StudySession>> GetByDateAsync(DateTime date);
        Task<StudySession?> GetByIdAsync(Guid id);


    }
}
