using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuWeb.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_RendererName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RendererName",
                table: "Renderers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RendererName",
                table: "Renderers");
        }
    }
}
