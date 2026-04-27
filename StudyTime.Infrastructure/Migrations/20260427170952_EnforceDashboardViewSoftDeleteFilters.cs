using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyTime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDashboardViewSoftDeleteFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR ALTER VIEW [dbo].[v_DashboardSummary]
                AS
                WITH TaskStats AS
                (
                    SELECT
                        t.[LessonId],
                        t.[UserId],
                        COUNT(1) AS [TotalTasks],
                        SUM(CASE WHEN t.[Status] = 1 THEN 1 ELSE 0 END) AS [CompletedTasks]
                    FROM [Tasks] t
                    WHERE
                        t.[IsDeleted] = 0
                        AND t.[LessonId] IS NOT NULL
                    GROUP BY
                        t.[LessonId],
                        t.[UserId]
                ),
                SessionStats AS
                (
                    SELECT
                        s.[LessonId],
                        s.[UserId],
                        SUM(CASE
                            WHEN s.[IsBreak] = 0
                            THEN DATEDIFF(MINUTE, s.[StartedAt], COALESCE(s.[EndedAt], SYSUTCDATETIME()))
                            ELSE 0
                        END) AS [TotalStudyMinutes],
                        SUM(CASE
                            WHEN s.[IsBreak] = 0 AND CAST(s.[StartedAt] AS DATE) = CAST(SYSUTCDATETIME() AS DATE)
                            THEN DATEDIFF(MINUTE, s.[StartedAt], COALESCE(s.[EndedAt], SYSUTCDATETIME()))
                            ELSE 0
                        END) AS [TodayStudyMinutes]
                    FROM [StudySessions] s
                    WHERE s.[IsDeleted] = 0
                    GROUP BY
                        s.[LessonId],
                        s.[UserId]
                )
                SELECT
                    l.[Id] AS [LessonId],
                    l.[UserId],
                    l.[Name] AS [LessonName],
                    COALESCE(ts.[TotalTasks], 0) AS [TotalTasks],
                    COALESCE(ts.[CompletedTasks], 0) AS [CompletedTasks],
                    COALESCE(ss.[TotalStudyMinutes], 0) AS [TotalStudyMinutes],
                    COALESCE(ss.[TodayStudyMinutes], 0) AS [TodayStudyMinutes]
                FROM [Lessons] l
                LEFT JOIN TaskStats ts ON ts.[LessonId] = l.[Id] AND ts.[UserId] = l.[UserId]
                LEFT JOIN SessionStats ss ON ss.[LessonId] = l.[Id] AND ss.[UserId] = l.[UserId]
                WHERE l.[IsDeleted] = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[v_DashboardSummary];");
        }
    }
}
