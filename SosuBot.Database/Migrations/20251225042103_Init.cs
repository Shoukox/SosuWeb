using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SosuBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Renderers",
                columns: table => new
                {
                    RendererId = table.Column<int>(type: "integer", nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BytesRendered = table.Column<long>(type: "bigint", nullable: false),
                    UsedGPU = table.Column<string>(type: "text", nullable: false),
                    UsedCPU = table.Column<string>(type: "text", nullable: false),
                    EncodingWithCPU = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Renderers", x => x.RendererId);
                });

            migrationBuilder.CreateTable(
                name: "RenderJobs",
                columns: table => new
                {
                    JobId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReplayPath = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    RenderingBy = table.Column<int>(type: "integer", nullable: false),
                    RendererId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenderJobs", x => x.JobId);
                    table.ForeignKey(
                        name: "FK_RenderJobs_Renderers_RendererId",
                        column: x => x.RendererId,
                        principalTable: "Renderers",
                        principalColumn: "RendererId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_RendererId",
                table: "RenderJobs",
                column: "RendererId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RenderJobs");

            migrationBuilder.DropTable(
                name: "Renderers");
        }
    }
}
