using System;
using System.Linq;
using Documents.Core.Rules;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Documents.Infrastructure.Migrations
{
    /// <summary>
    /// Spec 014: har table ka Id aur har FK int → uniqueidentifier, bina data loss ke. Hand-written (EF identity int ko
    /// GUID mein alter nahi kar sakta): naye GUID columns, fill (system lookup rows → DomainRules ke fixed GUIDs, baaki
    /// NEWID()), FKs purane int ids par join karke, phir purane FKs / indexes / PKs / columns hatao, rename, aur PKs /
    /// indexes / FKs snapshot jaise dobara. Sequences DocumentCategoryIds / RelationshipIds hatte hain.
    /// Down supported nahi — apply se pehle database backup lo. SQL: docs/migrations/008-ConvertIdsToGuid.sql.
    /// </summary>
    public partial class ConvertIdsToGuid : Migration
    {
        /// <summary>Har int id / FK column jo GUID banta hai (Nullable = lookup ka UserId, NULL = system row).</summary>
        private static readonly (string Table, string Column, bool Nullable)[] GuidColumns =
        [
            ("Users", "Id", false),
            ("Relationships", "Id", false),
            ("Relationships", "UserId", true),
            ("DocumentCategories", "Id", false),
            ("DocumentCategories", "UserId", true),
            ("FamilyMembers", "Id", false),
            ("FamilyMembers", "UserId", false),
            ("FamilyMembers", "RelationshipId", false),
            ("Documents", "Id", false),
            ("Documents", "UserId", false),
            ("Documents", "MemberId", false),
            ("Documents", "CategoryId", false),
            ("PasswordResetTokens", "Id", false),
            ("PasswordResetTokens", "UserId", false),
            ("RevokedTokens", "UserId", false),
            ("SetupState", "ClosedByUserId", false),
        ];

        /// <summary>Tables jinka PK "Id" hai (RevokedTokens = Jti, SetupState = tinyint singleton — wahi rehte hain).</summary>
        private static readonly string[] PrimaryKeyTables =
            ["Users", "Relationships", "DocumentCategories", "FamilyMembers", "Documents", "PasswordResetTokens"];

        private static readonly string[] ForeignKeys =
        [
            "FK_DocumentCategories_Users_UserId", "FK_Relationships_Users_UserId",
            "FK_FamilyMembers_Users_UserId", "FK_FamilyMembers_Relationships_RelationshipId",
            "FK_Documents_Users_UserId", "FK_Documents_FamilyMembers_MemberId", "FK_Documents_DocumentCategories_CategoryId",
            "FK_PasswordResetTokens_Users_UserId", "FK_RevokedTokens_Users_UserId", "FK_SetupState_Users_ClosedByUserId",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- 1. Naye nullable GUID columns (naam: New<Column>)
            foreach (var (table, column, _) in GuidColumns)
                migrationBuilder.AddColumn<Guid>(name: "New" + column, table: table, type: "uniqueidentifier", nullable: true);

            // --- 2. Fill. EXEC(): idempotent script ek hi batch hai — naye columns wala SQL warna compile par fail hota (007 jaisa).
            // FROZEN: DomainRules.SystemCategoryId / SystemRelationshipId aur SystemRelationships ka order kabhi mat badlo —
            // yeh applied migration unhi se SQL banata hai.
            // System rows (UserId NULL) ke fixed GUIDs DomainRules se — HasData seed bhi wahi values use karta hai.
            // Soft-deleted rows bhi (raw SQL par query filter nahi lagta).
            Exec(migrationBuilder, $"""
                UPDATE [Users] SET [NewId] = NEWID();

                UPDATE r SET r.[NewId] = v.[G]
                FROM [Relationships] r
                JOIN (VALUES {SystemValues(DomainRules.SystemRelationships.Count, DomainRules.SystemRelationshipId)}) v([OldId], [G])
                  ON v.[OldId] = r.[Id] AND r.[UserId] IS NULL;
                UPDATE [Relationships] SET [NewId] = NEWID() WHERE [NewId] IS NULL;
                UPDATE r SET r.[NewUserId] = u.[NewId] FROM [Relationships] r JOIN [Users] u ON u.[Id] = r.[UserId];

                UPDATE c SET c.[NewId] = v.[G]
                FROM [DocumentCategories] c
                JOIN (VALUES {SystemValues(7, DomainRules.SystemCategoryId)}) v([OldId], [G])
                  ON v.[OldId] = c.[Id] AND c.[UserId] IS NULL;
                UPDATE [DocumentCategories] SET [NewId] = NEWID() WHERE [NewId] IS NULL;
                UPDATE c SET c.[NewUserId] = u.[NewId] FROM [DocumentCategories] c JOIN [Users] u ON u.[Id] = c.[UserId];

                UPDATE [FamilyMembers] SET [NewId] = NEWID();
                UPDATE m SET m.[NewUserId] = u.[NewId] FROM [FamilyMembers] m JOIN [Users] u ON u.[Id] = m.[UserId];
                UPDATE m SET m.[NewRelationshipId] = r.[NewId] FROM [FamilyMembers] m JOIN [Relationships] r ON r.[Id] = m.[RelationshipId];

                UPDATE [Documents] SET [NewId] = NEWID();
                UPDATE d SET d.[NewUserId] = u.[NewId] FROM [Documents] d JOIN [Users] u ON u.[Id] = d.[UserId];
                UPDATE d SET d.[NewMemberId] = m.[NewId] FROM [Documents] d JOIN [FamilyMembers] m ON m.[Id] = d.[MemberId];
                UPDATE d SET d.[NewCategoryId] = c.[NewId] FROM [Documents] d JOIN [DocumentCategories] c ON c.[Id] = d.[CategoryId];

                UPDATE [PasswordResetTokens] SET [NewId] = NEWID();
                UPDATE t SET t.[NewUserId] = u.[NewId] FROM [PasswordResetTokens] t JOIN [Users] u ON u.[Id] = t.[UserId];

                UPDATE t SET t.[NewUserId] = u.[NewId] FROM [RevokedTokens] t JOIN [Users] u ON u.[Id] = t.[UserId];

                UPDATE s SET s.[NewClosedByUserId] = u.[NewId] FROM [SetupState] s JOIN [Users] u ON u.[Id] = s.[ClosedByUserId];
                """);

            // --- 3. Purane FKs, id wale indexes aur PKs hatao
            foreach (var name in ForeignKeys)
                migrationBuilder.DropForeignKey(name: name, table: name.Split('_')[1]);

            migrationBuilder.DropIndex(name: "IX_Documents_CategoryId", table: "Documents");
            migrationBuilder.DropIndex(name: "IX_Documents_MemberId", table: "Documents");
            migrationBuilder.DropIndex(name: "IX_Documents_UserId_CategoryId", table: "Documents");
            migrationBuilder.DropIndex(name: "IX_Documents_UserId_MemberId", table: "Documents");
            migrationBuilder.DropIndex(name: "IX_Documents_UserId_UploadedOn_Id", table: "Documents");
            migrationBuilder.DropIndex(name: "IX_FamilyMembers_RelationshipId", table: "FamilyMembers");
            migrationBuilder.DropIndex(name: "IX_FamilyMembers_UserId_Name", table: "FamilyMembers");
            migrationBuilder.DropIndex(name: "IX_Relationships_UserId_Name", table: "Relationships");
            migrationBuilder.DropIndex(name: "IX_DocumentCategories_UserId_Name", table: "DocumentCategories");
            migrationBuilder.DropIndex(name: "IX_PasswordResetTokens_UserId", table: "PasswordResetTokens");
            migrationBuilder.DropIndex(name: "IX_RevokedTokens_UserId", table: "RevokedTokens");
            migrationBuilder.DropIndex(name: "IX_SetupState_ClosedByUserId", table: "SetupState");

            foreach (var table in PrimaryKeyTables)
                migrationBuilder.DropPrimaryKey(name: "PK_" + table, table: table);

            // --- 4. Purane int columns hatao (DropColumn sequence wala DEFAULT constraint bhi hatata hai), naye ko wahi naam.
            // Koi FK value na mili ho (orphan) to NOT NULL fail → poori migration rollback, data safe.
            foreach (var (table, column, _) in GuidColumns)
                migrationBuilder.DropColumn(name: column, table: table);
            foreach (var (table, column, _) in GuidColumns)
                migrationBuilder.RenameColumn(name: "New" + column, table: table, newName: column);
            foreach (var (table, column, _) in GuidColumns.Where(c => !c.Nullable))
                migrationBuilder.AlterColumn<Guid>(
                    name: column, table: table, type: "uniqueidentifier", nullable: false,
                    oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);

            migrationBuilder.DropSequence(name: "DocumentCategoryIds");
            migrationBuilder.DropSequence(name: "RelationshipIds");

            // --- 5. PKs, indexes, FKs — model snapshot jaise (naam, filter, unique, descending, delete behaviour)
            foreach (var table in PrimaryKeyTables)
                migrationBuilder.AddPrimaryKey(name: "PK_" + table, table: table, column: "Id");

            migrationBuilder.CreateIndex(name: "IX_Documents_CategoryId", table: "Documents", column: "CategoryId");
            migrationBuilder.CreateIndex(name: "IX_Documents_MemberId", table: "Documents", column: "MemberId");
            migrationBuilder.CreateIndex(name: "IX_Documents_UserId_CategoryId", table: "Documents", columns: ["UserId", "CategoryId"]);
            migrationBuilder.CreateIndex(name: "IX_Documents_UserId_MemberId", table: "Documents", columns: ["UserId", "MemberId"]);
            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserId_UploadedOn_Id", table: "Documents",
                columns: ["UserId", "UploadedOn", "Id"], descending: [false, true, true]);
            migrationBuilder.CreateIndex(name: "IX_FamilyMembers_RelationshipId", table: "FamilyMembers", column: "RelationshipId");
            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_UserId_Name", table: "FamilyMembers",
                columns: ["UserId", "Name"], unique: true, filter: "[DeletedOn] IS NULL");
            migrationBuilder.CreateIndex(
                name: "IX_Relationships_UserId_Name", table: "Relationships",
                columns: ["UserId", "Name"], unique: true, filter: "[IsActive] = 1");
            migrationBuilder.CreateIndex(
                name: "IX_DocumentCategories_UserId_Name", table: "DocumentCategories",
                columns: ["UserId", "Name"], unique: true, filter: "[IsActive] = 1");
            migrationBuilder.CreateIndex(name: "IX_PasswordResetTokens_UserId", table: "PasswordResetTokens", column: "UserId");
            migrationBuilder.CreateIndex(name: "IX_RevokedTokens_UserId", table: "RevokedTokens", column: "UserId");
            migrationBuilder.CreateIndex(name: "IX_SetupState_ClosedByUserId", table: "SetupState", column: "ClosedByUserId");

            AddForeignKey(migrationBuilder, "DocumentCategories", "UserId", "Users", ReferentialAction.Restrict);
            AddForeignKey(migrationBuilder, "Relationships", "UserId", "Users", ReferentialAction.Restrict);
            AddForeignKey(migrationBuilder, "FamilyMembers", "UserId", "Users", ReferentialAction.Restrict);
            AddForeignKey(migrationBuilder, "FamilyMembers", "RelationshipId", "Relationships", ReferentialAction.Restrict);
            AddForeignKey(migrationBuilder, "Documents", "UserId", "Users", ReferentialAction.Restrict);
            AddForeignKey(migrationBuilder, "Documents", "MemberId", "FamilyMembers", ReferentialAction.Cascade);
            AddForeignKey(migrationBuilder, "Documents", "CategoryId", "DocumentCategories", ReferentialAction.Restrict);
            AddForeignKey(migrationBuilder, "PasswordResetTokens", "UserId", "Users", ReferentialAction.Cascade);
            AddForeignKey(migrationBuilder, "RevokedTokens", "UserId", "Users", ReferentialAction.Cascade);
            AddForeignKey(migrationBuilder, "SetupState", "ClosedByUserId", "Users", ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(
                "ConvertIdsToGuid cannot be reverted (GUIDs cannot be mapped back to the old int ids). Restore the database backup taken before applying it.");

        private static void AddForeignKey(MigrationBuilder mb, string table, string column, string principal, ReferentialAction onDelete) =>
            mb.AddForeignKey(
                name: $"FK_{table}_{principal}_{column}", table: table, column: column,
                principalTable: principal, principalColumn: "Id", onDelete: onDelete);

        /// <summary>"(1, CONVERT(uniqueidentifier, '…')), (2, …)" — purana int id → fixed GUID.</summary>
        private static string SystemValues(int count, Func<int, Guid> id) =>
            string.Join(", ", Enumerable.Range(1, count).Select(i => $"({i}, CONVERT(uniqueidentifier, '{id(i)}'))"));

        /// <summary>SQL ko EXEC(N'…') mein chalata hai (quotes double).</summary>
        private static void Exec(MigrationBuilder mb, string sql) =>
            mb.Sql("EXEC(N'" + sql.Replace("'", "''") + "');");
    }
}
