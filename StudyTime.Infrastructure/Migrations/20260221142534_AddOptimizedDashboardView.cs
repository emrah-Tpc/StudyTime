using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyTime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimizedDashboardView : Migration
    {
        // ── CTE + JOIN yaklaşımı: correlated subquery yok, tek geçiş ────────
        private const string CreateViewSql = """
            CREATE OR ALTER VIEW v_DashboardSummary AS
            WITH SessionStats AS (
                SELECT
                    LessonId,
                    SUM(DATEDIFF(MINUTE, StartedAt, ISNULL(EndedAt, GETUTCDATE()))) AS TotalStudyMinutes,
                    SUM(
                        CASE
                            WHEN CAST(StartedAt AS DATE) = CAST(GETUTCDATE() AS DATE)
                            THEN DATEDIFF(MINUTE, StartedAt, ISNULL(EndedAt, GETUTCDATE()))
                            ELSE 0
                        END
                    ) AS TodayStudyMinutes
                FROM StudySessions
                WHERE IsBreak = 0
                GROUP BY LessonId
            ),
            TaskStats AS (
                SELECT
                    LessonId,
                    COUNT(*)                                             AS TotalTasks,
                    SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)         AS CompletedTasks
                FROM Tasks
                WHERE IsDeleted = 0
                GROUP BY LessonId
            )
            SELECT
                l.Id                                    AS LessonId,
                l.Name                                  AS LessonName,
                ISNULL(t.TotalTasks,        0)          AS TotalTasks,
                ISNULL(t.CompletedTasks,    0)          AS CompletedTasks,
                ISNULL(s.TotalStudyMinutes, 0)          AS TotalStudyMinutes,
                ISNULL(s.TodayStudyMinutes, 0)          AS TodayStudyMinutes
            FROM Lessons l
            LEFT JOIN TaskStats    t ON t.LessonId = l.Id
            LEFT JOIN SessionStats s ON s.LessonId = l.Id
            WHERE l.IsDeleted = 0;
            """;

        // İndexler (hem Tasks hem StudySessions için)
        private const string CreateIndexesSql = """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tasks_LessonId_Status_IsDeleted')
            CREATE NONCLUSTERED INDEX IX_Tasks_LessonId_Status_IsDeleted
                ON Tasks (LessonId, Status) INCLUDE (IsDeleted);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudySessions_LessonId_IsBreak_Dates')
            CREATE NONCLUSTERED INDEX IX_StudySessions_LessonId_IsBreak_Dates
                ON StudySessions (LessonId, IsBreak, StartedAt) INCLUDE (EndedAt);
            """;

        private const string DropViewSql   = "DROP VIEW IF EXISTS v_DashboardSummary;";
        private const string DropIndexesSql = """
            DROP INDEX IF EXISTS IX_Tasks_LessonId_Status_IsDeleted           ON Tasks;
            DROP INDEX IF EXISTS IX_StudySessions_LessonId_IsBreak_Dates ON StudySessions;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CreateViewSql);
            migrationBuilder.Sql(CreateIndexesSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropIndexesSql);
            migrationBuilder.Sql(DropViewSql);
        }
    }
}
