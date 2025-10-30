using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Application.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAddressTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Users_Addresses_AddressId", table: "Users");

            migrationBuilder.DropTable(name: "Addresses");

            migrationBuilder.DropIndex(name: "IX_Users_AddressId", table: "Users");

            migrationBuilder.DropColumn(name: "AddressId", table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AddressId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                }
            );

            migrationBuilder.InsertData(
                table: "Addresses",
                columns: new[] { "Id", "AddressLine" },
                values: new object[,]
                {
                    { new Guid("3d5f357c-633c-4bf0-96ee-7cccdb4bd81c"), "Antonínova 4379, 760 01 Zlín 1" },
                    { new Guid("a8e8f5a9-63ed-4a52-9e35-02f2bd7ceaca"), "Panelák v ružovej záhrade" },
                }
            );

            migrationBuilder.CreateIndex(name: "IX_Users_AddressId", table: "Users", column: "AddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Addresses_AddressId",
                table: "Users",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }
    }
}
