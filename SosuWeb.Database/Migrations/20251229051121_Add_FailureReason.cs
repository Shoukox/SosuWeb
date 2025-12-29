using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuWeb.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_FailureReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "RenderJobs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "RenderJobs");
        }
    }
}
