using StudyTime.Domain.Entities;

namespace StudyTime.Application.Interfaces
{
    /// <summary>
    /// v_DashboardSummary view'undan ders bazlı özet verileri çekmek için repository arayüzü.
    /// </summary>
    public interface IDashboardRepository
    {
        Task<List<DashboardSummaryView>> GetDashboardSummariesAsync();
    }
}
