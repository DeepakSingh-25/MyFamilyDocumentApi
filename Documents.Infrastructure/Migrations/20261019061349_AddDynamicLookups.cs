using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Documents.Infrastructure.Migrations
{
    /// <summary>
    /// Spec 013: user ki apni categories / relationships. FamilyMembers.Relationship (text) → RelationshipId (FK)
    /// bina data loss ke — system naam se match, warna us user ka custom relationship banta hai.
    /// Generated migration hand-edited (EF pehle column drop kar deta tha). SQL: docs/migrations/007-AddDynamicLookups.sql.
    /// </summary>
    public partial class AddDynamicLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "DocumentCategoryIds",
                startValue: 101L);

            migrationBuilder.CreateSequence<int>(
                name: "RelationshipIds",
                startValue: 101L);

            // --- DocumentCategories: system rows wahi ids (1–7) rakhte hain; naye ids sequence se
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "DocumentCategories",
                type: "int",
                nullable: false,
                defaultValueSql: "NEXT VALUE FOR [DocumentCategoryIds]",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "DocumentCategories",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "DocumentCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "DocumentCategories",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "DocumentCategories",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "IsActive", "SortOrder", "UserId" },
                values: new object[] { true, 10, null });

            migrationBuilder.UpdateData(
                table: "DocumentCategories",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "IsActive", "SortOrder", "UserId" },
                values: new object[] { true, 20, null });

            migrationBuilder.UpdateData(
                table: "DocumentCategories",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "IsActive", "SortOrder", "UserId" },
                values: new object[] { true, 30, null });

            migrationBuilder.UpdateData(
                table: "DocumentCategories",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "IsActive", "SortOrder", "UserId" },
                values: new object[] { true, 40, null });

            migrationBuilder.UpdateData(
                table: "DocumentCategories",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "IsActive", "SortOrder", "UserId" },
                values: new object[] { true, 50, null });

            migrationBuilder.UpdateData(
                table: "DocumentCategories",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "IsActive", "SortOrder", "UserId" },
                values: new object[] { true, 60, null });

            migrationBuilder.UpdateData(
                table: "DocumentCategories",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "IsActive", "SortOrder", "UserId" },
                values: new object[] { true, 1000, null });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCategories_UserId_Name",
                table: "DocumentCategories",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentCategories_Users_UserId",
                table: "DocumentCategories",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // --- Relationships (naya lookup) + 13 system rows
            migrationBuilder.CreateTable(
                name: "Relationships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, defaultValueSql: "NEXT VALUE FOR [RelationshipIds]"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Relationships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Relationships",
                columns: new[] { "Id", "IsActive", "Name", "SortOrder", "UserId" },
                values: new object[,]
                {
                    { 1, true, "Self", 10, null },
                    { 2, true, "Spouse", 20, null },
                    { 3, true, "Father", 30, null },
                    { 4, true, "Mother", 40, null },
                    { 5, true, "Son", 50, null },
                    { 6, true, "Daughter", 60, null },
                    { 7, true, "Brother", 70, null },
                    { 8, true, "Sister", 80, null },
                    { 9, true, "Grandfather", 90, null },
                    { 10, true, "Grandmother", 100, null },
                    { 11, true, "Daughter-in-law", 110, null },
                    { 12, true, "Son-in-law", 120, null },
                    { 13, true, "Other", 1000, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Relationships_UserId_Name",
                table: "Relationships",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "[IsActive] = 1");

            // --- FamilyMembers: text → FK. Pehle nullable column, data copy, phir NOT NULL, phir purana column hatao.
            migrationBuilder.AddColumn<int>(
                name: "RelationshipId",
                table: "FamilyMembers",
                type: "int",
                nullable: true);

            // Koi text system list mein nahi (purana data / haath se likha) → us user ka custom relationship, taaki kuch na khoye.
            // Soft-deleted members bhi (raw SQL par query filter nahi lagta).
            // EXEC(): EF 10 ka idempotent script ek hi batch hai — naye column wala SQL warna compile time par fail hota.
            migrationBuilder.Sql("""
                EXEC(N'
                INSERT INTO [Relationships] ([UserId], [Name], [SortOrder], [IsActive])
                SELECT DISTINCT m.[UserId], LTRIM(RTRIM(m.[Relationship])), 500, 1
                FROM [FamilyMembers] m
                WHERE LTRIM(RTRIM(m.[Relationship])) <> ''''
                  AND NOT EXISTS (SELECT 1 FROM [Relationships] r
                                  WHERE r.[UserId] IS NULL AND r.[Name] = LTRIM(RTRIM(m.[Relationship])));
                ');
                """);

            migrationBuilder.Sql("""
                EXEC(N'
                UPDATE m SET m.[RelationshipId] = r.[Id]
                FROM [FamilyMembers] m
                JOIN [Relationships] r
                  ON r.[Name] = LTRIM(RTRIM(m.[Relationship]))
                 AND (r.[UserId] IS NULL OR r.[UserId] = m.[UserId])
                 AND r.[IsActive] = 1;
                -- Khaali text (sirf SQL se ho sakta tha) → "Other" (13)
                UPDATE [FamilyMembers] SET [RelationshipId] = 13 WHERE [RelationshipId] IS NULL;
                ');
                """);

            migrationBuilder.AlterColumn<int>(
                name: "RelationshipId",
                table: "FamilyMembers",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Relationship",
                table: "FamilyMembers");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_RelationshipId",
                table: "FamilyMembers",
                column: "RelationshipId");

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMembers_Relationships_RelationshipId",
                table: "FamilyMembers",
                column: "RelationshipId",
                principalTable: "Relationships",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // --- FamilyMembers: FK → text (naam wapas copy)
            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMembers_Relationships_RelationshipId",
                table: "FamilyMembers");

            migrationBuilder.DropIndex(
                name: "IX_FamilyMembers_RelationshipId",
                table: "FamilyMembers");

            migrationBuilder.AddColumn<string>(
                name: "Relationship",
                table: "FamilyMembers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql("""
                EXEC(N'
                UPDATE m SET m.[Relationship] = r.[Name]
                FROM [FamilyMembers] m
                JOIN [Relationships] r ON r.[Id] = m.[RelationshipId];
                ');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Relationship",
                table: "FamilyMembers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "RelationshipId",
                table: "FamilyMembers");

            migrationBuilder.DropTable(
                name: "Relationships");

            // --- DocumentCategories: purane schema mein apni categories nahi hoti. Unke documents "Other Documents" (7)
            // mein jaate hain aur custom rows hatti hain — warna UserId hatne ke baad wo sab users ko dikhti.
            migrationBuilder.Sql("""
                EXEC(N'
                UPDATE d SET d.[CategoryId] = 7
                FROM [Documents] d
                JOIN [DocumentCategories] c ON c.[Id] = d.[CategoryId]
                WHERE c.[UserId] IS NOT NULL;
                DELETE FROM [DocumentCategories] WHERE [UserId] IS NOT NULL;
                ');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentCategories_Users_UserId",
                table: "DocumentCategories");

            migrationBuilder.DropIndex(
                name: "IX_DocumentCategories_UserId_Name",
                table: "DocumentCategories");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "DocumentCategories");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "DocumentCategories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "DocumentCategories");

            // Default (sequence) pehle hatao, sequence baad mein
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "DocumentCategories",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValueSql: "NEXT VALUE FOR [DocumentCategoryIds]");

            migrationBuilder.DropSequence(
                name: "DocumentCategoryIds");

            migrationBuilder.DropSequence(
                name: "RelationshipIds");
        }
    }
}
