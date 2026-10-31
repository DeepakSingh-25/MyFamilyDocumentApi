using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Documents.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordChangedOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedOn",
                table: "Users",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordChangedOn",
                table: "Users");
        }
    }
}
