using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuWeb.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_RendererCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RendererCredentials",
                columns: table => new
                {
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    ClientSecretHash = table.Column<string>(type: "text", nullable: false),
                    ClientSecretSalt = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RendererCredentials", x => x.ClientId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RendererCredentials");
        }
    }
}
