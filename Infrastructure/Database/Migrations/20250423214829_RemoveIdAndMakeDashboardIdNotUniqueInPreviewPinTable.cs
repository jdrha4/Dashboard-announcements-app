using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIdAndMakeDashboardIdNotUniqueInPreviewPinTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DashboardPreviewPins",
                table: "DashboardPreviewPins");

            migrationBuilder.DropIndex(
                name: "IX_DashboardPreviewPins_DashboardId",
                table: "DashboardPreviewPins");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "DashboardPreviewPins");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DashboardPreviewPins",
                table: "DashboardPreviewPins",
                column: "Pin");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardPreviewPins_DashboardId",
                table: "DashboardPreviewPins",
                column: "DashboardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DashboardPreviewPins",
                table: "DashboardPreviewPins");

            migrationBuilder.DropIndex(
                name: "IX_DashboardPreviewPins_DashboardId",
                table: "DashboardPreviewPins");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "DashboardPreviewPins",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_DashboardPreviewPins",
                table: "DashboardPreviewPins",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardPreviewPins_DashboardId",
                table: "DashboardPreviewPins",
                column: "DashboardId",
                unique: true);
        }
    }
}
