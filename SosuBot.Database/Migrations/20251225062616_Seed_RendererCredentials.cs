using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Seed_RendererCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "RendererCredentials",
                columns: new[] { "ClientId", "ClientSecretHash", "ClientSecretSalt", "CreatedAt" },
                values: new object[] { 1234, "9a9b4043565915eea98c07ff06bcb15e615a2477a4b04fbdd8645a7a4da531027c740aa9a865f34df497a1d0665658b1d9757ebe3347b33c650175a0ba2a2eb4", "sosubot_renderer1234", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RendererCredentials",
                keyColumn: "ClientId",
                keyValue: 1234);
        }
    }
}
