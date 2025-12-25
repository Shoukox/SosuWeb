using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_RenderJob_ProgressPercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsComplete",
                table: "RenderJobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "ProgressPercent",
                table: "RenderJobs",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsComplete",
                table: "RenderJobs");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "RenderJobs");
        }
    }
}
