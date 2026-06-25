using StudyTime.Application.DTOs.StudySessions;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;

namespace StudyTime.Application.Services
{
    public class StudySessionService
    {
        private readonly IStudySessionRepository _studySessionRepository;
        private readonly ILessonRepository _lessonRepository;

        public StudySessionService(IStudySessionRepository studySessionRepository, ILessonRepository lessonRepository)
        {
            _studySessionRepository = studySessionRepository;
            _lessonRepository = lessonRepository;
        }

        // ▶ START
        public async Task<Guid> StartAsync(StartStudySessionDto dto, string userId)
        {
            var activeSession = await _studySessionRepository.GetActiveSessionAsync(userId);
            if (activeSession != null)
            {
                throw new InvalidOperationException("ACTIVE_SESSION_EXISTS");
            }

            // Ders var mı kontrolü
            var lessonExists = await _lessonRepository.ExistsAsync(dto.LessonId);
            if (!lessonExists)
            {
                throw new KeyNotFoundException("LESSON_NOT_FOUND");
            }

            var session = new StudySession(dto.LessonId, dto.TaskId, dto.IsBreak);
            session.UserId = userId;
            session.Start();
            await _studySessionRepository.AddAsync(session);
            return session.Id;
        }

        public async Task<StudySession?> GetActiveSessionAsync(string userId)
        {
            return await _studySessionRepository.GetActiveSessionAsync(userId);
        }

        // ⏹ STOP
        public async Task StopAsync(Guid sessionId, DateTime? updatedAt = null)
        {
            var session = await _studySessionRepository.GetByIdAsync(sessionId);
            if (session is null)
                return; // Idempotent stop: istemci tarafinda stale/local id gelebilir.

            if (updatedAt.HasValue && session.UpdatedAt.HasValue && updatedAt < session.UpdatedAt) 
                throw new StudyTime.Application.Exceptions.DataConflictException("Session has been modified by another client.");

            session.Stop();
            session.UpdatedAt = updatedAt ?? DateTime.UtcNow;
            await _studySessionRepository.UpdateAsync(session);
        }
        // StudyTime.Application.Services.StudySessionService.cs içine ekle:

        // ⏸ PAUSE (Duraklat)
        public async Task PauseAsync(Guid sessionId, DateTime? updatedAt = null)
        {
            var session = await _studySessionRepository.GetByIdAsync(sessionId);
            if (session is null)
                return;

            if (updatedAt.HasValue && session.UpdatedAt.HasValue && updatedAt < session.UpdatedAt) 
                throw new StudyTime.Application.Exceptions.DataConflictException("Session has been modified by another client.");

            session.Pause();
            session.UpdatedAt = updatedAt ?? DateTime.UtcNow;
            await _studySessionRepository.UpdateAsync(session);
        }

        // ▶️ RESUME (Devam Et)
        public async Task ResumeAsync(Guid sessionId, DateTime? updatedAt = null)
        {
            var session = await _studySessionRepository.GetByIdAsync(sessionId);
            if (session is null)
                return;

            if (updatedAt.HasValue && session.UpdatedAt.HasValue && updatedAt < session.UpdatedAt) 
                throw new StudyTime.Application.Exceptions.DataConflictException("Session has been modified by another client.");

            session.Resume();
            session.UpdatedAt = updatedAt ?? DateTime.UtcNow;
            await _studySessionRepository.UpdateAsync(session);
        }
        // 📊 TODAY TOTAL
        public async Task<TodayStudyTotalDto> GetTodayTotalAsync()
        {
            var today = DateTime.Now.Date;
            var sessions = await _studySessionRepository.GetByDateAsync(today);

            var totalMinutes = sessions.Sum(s => (int)s.TotalActiveDuration.TotalMinutes);

            return new TodayStudyTotalDto
            {
                TotalMinutes = totalMinutes
            };
        }
    }
}