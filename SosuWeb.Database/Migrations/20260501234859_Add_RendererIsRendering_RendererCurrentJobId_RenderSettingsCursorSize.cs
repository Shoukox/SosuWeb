using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuWeb.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_RendererIsRendering_RendererCurrentJobId_RenderSettingsCursorSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentJobId",
                table: "Renderers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsRendering",
                table: "Renderers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PerformancePoints",
                table: "Renderers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentJobId",
                table: "Renderers");

            migrationBuilder.DropColumn(
                name: "IsRendering",
                table: "Renderers");

            migrationBuilder.DropColumn(
                name: "PerformancePoints",
                table: "Renderers");
        }
    }
}
