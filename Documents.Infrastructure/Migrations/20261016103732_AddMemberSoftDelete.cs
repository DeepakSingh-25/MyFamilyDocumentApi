using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Documents.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FamilyMembers_UserId_Name",
                table: "FamilyMembers");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "FamilyMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_UserId_Name",
                table: "FamilyMembers",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "[DeletedOn] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FamilyMembers_UserId_Name",
                table: "FamilyMembers");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "FamilyMembers");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_UserId_Name",
                table: "FamilyMembers",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }
    }
}
