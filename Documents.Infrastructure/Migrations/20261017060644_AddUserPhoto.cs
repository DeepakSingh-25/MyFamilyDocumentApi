using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Documents.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoKey",
                table: "Users",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoType",
                table: "Users",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoKey",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhotoType",
                table: "Users");
        }
    }
}
