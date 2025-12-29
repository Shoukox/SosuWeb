using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuWeb.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_VideoUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VideoUri",
                table: "RenderJobs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoUri",
                table: "RenderJobs");
        }
    }
}
