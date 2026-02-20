using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyTime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsBreakToStudySession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBreak",
                table: "StudySessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_LessonId",
                table: "StudySessions",
                column: "LessonId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudySessions_Lessons_LessonId",
                table: "StudySessions",
                column: "LessonId",
                principalTable: "Lessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudySessions_Lessons_LessonId",
                table: "StudySessions");

            migrationBuilder.DropIndex(
                name: "IX_StudySessions_LessonId",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "IsBreak",
                table: "StudySessions");
        }
    }
}
