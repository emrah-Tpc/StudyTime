using Microsoft.EntityFrameworkCore;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;
using StudyTime.Infrastructure.Persistence;

namespace StudyTime.Infrastructure.Repositories
{
    public class DashboardRepository(StudyTimeDbContext context) : IDashboardRepository
    {
        public async Task<List<DashboardSummaryView>> GetDashboardSummariesAsync()
        {
            return await context.DashboardSummaries
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
