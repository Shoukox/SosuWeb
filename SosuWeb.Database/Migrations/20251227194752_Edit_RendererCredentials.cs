using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuWeb.Database.Migrations
{
    /// <inheritdoc />
    public partial class Edit_RendererCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientSecretSalt",
                table: "RendererCredentials");

            migrationBuilder.UpdateData(
                table: "RendererCredentials",
                keyColumn: "ClientId",
                keyValue: 1234,
                column: "ClientSecretHash",
                value: "AQAAAAIAAYagAAAAEJ4Em39CRb34cIHySVUfu5CzCfxl2OgnbI89kooDxJSAU5Y6/54h7o5jPheh3VZGFQ==");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientSecretSalt",
                table: "RendererCredentials",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "RendererCredentials",
                keyColumn: "ClientId",
                keyValue: 1234,
                columns: new[] { "ClientSecretHash", "ClientSecretSalt" },
                values: new object[] { "9a9b4043565915eea98c07ff06bcb15e615a2477a4b04fbdd8645a7a4da531027c740aa9a865f34df497a1d0665658b1d9757ebe3347b33c650175a0ba2a2eb4", "sosubot_renderer1234" });
        }
    }
}
