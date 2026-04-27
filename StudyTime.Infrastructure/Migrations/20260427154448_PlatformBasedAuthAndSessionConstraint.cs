using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyTime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformBasedAuthAndSessionConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudySessions_UserId",
                table: "StudySessions");

            migrationBuilder.RenameColumn(
                name: "RefreshTokenExpiryTime",
                table: "AspNetUsers",
                newName: "MobileRefreshTokenExpiryTime");

            migrationBuilder.RenameColumn(
                name: "RefreshToken",
                table: "AspNetUsers",
                newName: "MobileRefreshToken");

            migrationBuilder.RenameColumn(
                name: "HwidAssignedAt",
                table: "AspNetUsers",
                newName: "DesktopRefreshTokenExpiryTime");

            migrationBuilder.RenameColumn(
                name: "CurrentActiveHwid",
                table: "AspNetUsers",
                newName: "MobileHwid");

            migrationBuilder.AddColumn<string>(
                name: "DesktopHwid",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DesktopRefreshToken",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_UserId",
                table: "StudySessions",
                column: "UserId",
                unique: true,
                filter: "[EndedAt] IS NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudySessions_UserId",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "DesktopHwid",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DesktopRefreshToken",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "MobileRefreshTokenExpiryTime",
                table: "AspNetUsers",
                newName: "RefreshTokenExpiryTime");

            migrationBuilder.RenameColumn(
                name: "MobileRefreshToken",
                table: "AspNetUsers",
                newName: "RefreshToken");

            migrationBuilder.RenameColumn(
                name: "MobileHwid",
                table: "AspNetUsers",
                newName: "CurrentActiveHwid");

            migrationBuilder.RenameColumn(
                name: "DesktopRefreshTokenExpiryTime",
                table: "AspNetUsers",
                newName: "HwidAssignedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_UserId",
                table: "StudySessions",
                column: "UserId");
        }
    }
}
