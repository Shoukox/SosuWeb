using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuWeb.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_RendererClientVersionReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastReportedClientRendererVersion",
                table: "Renderers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReportedClientRendererVersionAt",
                table: "Renderers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastReportedClientRendererVersion",
                table: "Renderers");

            migrationBuilder.DropColumn(
                name: "LastReportedClientRendererVersionAt",
                table: "Renderers");
        }
    }
}
