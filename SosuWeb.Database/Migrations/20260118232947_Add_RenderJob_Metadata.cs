using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuWeb.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_RenderJob_Metadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MapName",
                table: "RenderJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlayerName",
                table: "RenderJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VideoThumbnailLocalPath",
                table: "RenderJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VideoThumbnailUri",
                table: "RenderJobs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MapName",
                table: "RenderJobs");

            migrationBuilder.DropColumn(
                name: "PlayerName",
                table: "RenderJobs");

            migrationBuilder.DropColumn(
                name: "VideoThumbnailLocalPath",
                table: "RenderJobs");

            migrationBuilder.DropColumn(
                name: "VideoThumbnailUri",
                table: "RenderJobs");
        }
    }
}
