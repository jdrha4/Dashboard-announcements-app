using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddExpiryOptionToDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxExpiryDate",
                table: "Dashboards");

            migrationBuilder.AddColumn<int>(
                name: "ExpiryOption",
                table: "Dashboards",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiryOption",
                table: "Dashboards");

            migrationBuilder.AddColumn<DateTime>(
                name: "MaxExpiryDate",
                table: "Dashboards",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
