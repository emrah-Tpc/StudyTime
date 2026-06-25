using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyTime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureNotificationsIsDeletedColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Notifications', 'IsDeleted') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Notifications]
                    ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_Notifications_IsDeleted] DEFAULT(0);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Notifications', 'IsDeleted') IS NOT NULL
                BEGIN
                    DECLARE @constraintName nvarchar(128);
                    SELECT @constraintName = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                    INNER JOIN sys.tables t ON t.object_id = c.object_id
                    WHERE t.name = 'Notifications' AND c.name = 'IsDeleted';

                    IF @constraintName IS NOT NULL
                        EXEC('ALTER TABLE [dbo].[Notifications] DROP CONSTRAINT [' + @constraintName + ']');

                    ALTER TABLE [dbo].[Notifications] DROP COLUMN [IsDeleted];
                END
                """);
        }
    }
}
