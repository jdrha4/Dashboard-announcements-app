using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmForUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confirmed",
                table: "EmailConfirmationTokens");

            migrationBuilder.AddColumn<bool>(
                name: "Confirmed",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confirmed",
                table: "Users");

            migrationBuilder.AddColumn<bool>(
                name: "Confirmed",
                table: "EmailConfirmationTokens",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
