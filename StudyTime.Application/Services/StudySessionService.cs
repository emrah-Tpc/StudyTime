using StudyTime.Application.DTOs.StudySessions;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;

namespace StudyTime.Application.Services
{
    public class StudySessionService
    {
        private readonly IStudySessionRepository _studySessionRepository;

        public StudySessionService(IStudySessionRepository studySessionRepository)
        {
            _studySessionRepository = studySessionRepository;
        }

        // ▶ START
        public async Task<Guid> StartAsync(StartStudySessionDto dto)
        {
            var session = new StudySession(dto.LessonId, dto.TaskId, dto.IsBreak);
            session.Start();
            await _studySessionRepository.AddAsync(session);
            return session.Id;
        }

        // ⏹ STOP
        public async Task StopAsync(Guid sessionId)
        {
            var session = await _studySessionRepository.GetByIdAsync(sessionId);
            if (session is null)
                throw new InvalidOperationException("Study session not found.");

            session.Stop();
            await _studySessionRepository.UpdateAsync(session);
        }
        // StudyTime.Application.Services.StudySessionService.cs içine ekle:

        // ⏸ PAUSE (Duraklat)
        public async Task PauseAsync(Guid sessionId)
        {
            var session = await _studySessionRepository.GetByIdAsync(sessionId);
            if (session is null)
                throw new InvalidOperationException("Study session not found.");

            session.Pause();
            await _studySessionRepository.UpdateAsync(session);
        }

        // ▶️ RESUME (Devam Et)
        public async Task ResumeAsync(Guid sessionId)
        {
            var session = await _studySessionRepository.GetByIdAsync(sessionId);
            if (session is null)
                throw new InvalidOperationException("Study session not found.");

            session.Resume();
            await _studySessionRepository.UpdateAsync(session);
        }
        // 📊 TODAY TOTAL
        public async Task<TodayStudyTotalDto> GetTodayTotalAsync()
        {
            var today = DateTime.UtcNow.Date;
            var sessions = await _studySessionRepository.GetByDateAsync(today);

            var totalMinutes = sessions.Sum(s => (int)s.TotalActiveDuration.TotalMinutes);

            return new TodayStudyTotalDto
            {
                TotalMinutes = totalMinutes
            };
        }
    }
}