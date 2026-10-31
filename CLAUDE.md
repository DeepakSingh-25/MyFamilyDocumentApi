# My Family Documents API — guide for Claude

> Backend for the Angular app in `../frontend`. .NET 10, Clean Architecture with
> **MediatR + repositories + MVC controllers**, EF Core 10 on SQL Server, JWT auth.
> Solution: `Documents.Api.sln`. Structure copied from the reference API `D:\Working\work\API`.

Cite `file:line`. Don't invent. Grep before assuming something exists. Flag unknowns.

## What exists (verified 2026-10-16)

| Project | Holds | References |
|---|---|---|
| `Documents.Core` | `Entities/` (User, SetupState, PasswordResetToken, FamilyMember, Relationship, DocumentCategory, Document), `Interfaces/` (5 repositories + hasher, token, email, file storage), `Models/` (DTOs, `Result`), `Options/`, `Rules/` (`DomainRules`, `FileRules` — shared with the web app) | nothing (MS.Extensions abstractions only) |
| `Documents.Application` | `Commands/`, `Queries/` (one use case per file), `Common/` (validation, tokens, local clock); MediatR 12.3 | Core |
| `Documents.Infrastructure` | `Data/` (AppDbContext, `Configurations/`), `Repositories/`, `Services/` (BCrypt, JWT, SMTP, local file storage), `Migrations/InitialCreate`, JWT bearer with `pwv` check | Application |
| `Documents.Api` | Controllers: Auth, Account (profile), Members, Categories, Relationships, Documents, Dashboard; `ApiControllerBase` (Result → ProblemDetails); `RateLimits.cs`; `Program.cs` | Application, Infrastructure |

Endpoint contract, data model and decisions: `../docs/specs/001-api-real-database.md` (approved).

DI: each layer has `DependencyInjection.cs` (`AddCoreDI`, `AddApplicationDI`, `AddInfrastructureDI`),
chained by `Documents.Api/DependencyInjection.cs` → `AddAppDI`.

**Tests:** `Documents.Api.Tests` (xUnit + `WebApplicationFactory<Program>`, added 2026-10-16 for spec 007) runs the real
pipeline against a **separate** SQL Server database `Documents_Tests` (same server/login as user-secrets, only the catalog
changes; `ApiFactory` refuses any other database, migrates it and wipes its rows at start). `dotnet test Documents.Api.Tests`.

**Soft delete:** `FamilyMembers.DeletedOn` + global query filters on members and documents (spec 007 §B.5). Never add
`IgnoreQueryFilters()` to app code; restore is SQL-only (`../docs/migrations/004-AddMemberSoftDelete.sql`).

**Absent today — do not assume them:** FluentValidation, refresh tokens,
`Directory.Build.props`, CPM. Request DTOs use DataAnnotations; the multipart document form lives in
`Documents.Api/Models` because `IFormFile` is an ASP.NET type.

## What the app must do (product spec)

- Tables: `Users`, `FamilyMembers`, `Relationships`, `DocumentCategories` (7 system rows: Identity, Bank & Financial,
  Medical, Property & Home, Vehicle, Education, Other — plus each user's own; same for 13 system relationships, spec 013), `Documents`.
- Every member and document belongs to one user; **users must never reach another user's data**.
- Auth: email + password (BCrypt), sign-up only with a one-time setup code and closed afterwards,
  forgot/reset password, change password, logout.
- Documents: PDF/JPG/PNG upload, replace file, download, preview, search/filter by member, category
  and name. Medical documents carry optional hospital/doctor and report date.
- Files in **private storage** (not `wwwroot`); SQL Server holds metadata + storage key. Validate
  extension, size and real content type. HTTPS in deployment. Backups for DB and files.
- The frontend mock (`../frontend/src/app/core/mock/`, `core/services/`) already implements these
  rules — it is the reference behaviour for the API.

## The golden path for a new feature

1. Entity → `Documents.Core/Entities/<Name>Entity.cs`; add `DbSet` to `AppDbContext`.
2. Port → `Documents.Core/Interfaces/I<Name>Repository.cs` (methods take `userId`).
3. Adapter → `Documents.Infrastructure/Repositories/<Name>Repository.cs`; register `AddScoped` in
   `Documents.Infrastructure/DependencyInjection.cs`.
4. Use cases → `Documents.Application/Commands/<Verb><Name>Command.cs` and `Queries/Get<Name>...Query.cs`
   — `record ... : IRequest<T>` + `...Handler` in the same file.
5. Endpoint → `Documents.Api/Controllers/<Name>Controller.cs`, `[Route("api/[controller]")]`,
   `[Authorize]`, inject `ISender`, one `sender.Send(...)` per action.
6. Migration → `database-migration-specialist` agent.
7. Build: `dotnet build Documents.Api.sln` — 0 errors, no new warnings.

## Rules that are easy to break

- Layer direction: Core ← Application ← Infrastructure ← Api. **EF Core and file I/O only in Infrastructure.**
- Ownership filter on every member/document query; cross-user id → 404.
- Bind request DTOs from `Core/Models`, never entities.
- Never log passwords, tokens, reset links or document numbers.
- `Jwt:SecretKey` lives in **user-secrets** (`UserSecretsId` in `Documents.Api.csproj`), not in
  `appsettings*.json`. On a new machine: `dotnet user-secrets set "Jwt:SecretKey" "<32+ chars>" --project Documents.Api`.
- New packages: same version family (EF/Extensions `8.0.x`, IdentityModel `7.1.x` to match JwtBearer 8).
- Comments mix Hinglish and English; match the surrounding file.

## JSON casing (decided)

**camelCase** (ASP.NET default) — matches the frontend models. Errors are ProblemDetails; the text
to show the user is in `detail`.

## Configuration

`Jwt:SecretKey`, `Signup:SetupCode` and the real `ConnectionStrings:DefaultConnection` live in
**user-secrets** (never in git, never print them). `appsettings.Development.json` holds
`Storage:RootPath` (`D:\FamilyDocumentsData\files`), Gmail SMTP host/port (`smtp.gmail.com:587`, since 2026-10-24;
`Smtp:User`, `Smtp:Password` = Gmail app password and `Smtp:From` live in user-secrets; set `Smtp:PickupDirectory`
to `D:\FamilyDocumentsData\mail-outbox` to write .eml files instead of sending), `App:WebBaseUrl` and `Cors:Origins`.

## Database

Server `192.168.120.17`, database `Documents` (connection string in user-secrets). `InitialCreate`, `AddRevokedTokens`, `AddPasswordChangedOn`, `AddMemberSoftDelete`, `AddDocumentSoftDelete`
applied 2026-10-16, `AddUserPhoto` (Users.PhotoKey / PhotoType, profile photo) applied 2026-10-17 (server-side logout revokes a token by `jti`); `AddDynamicLookups` (spec 013: `Relationships` table, `FamilyMembers.RelationshipId` FK, user-owned categories; SQL in `../docs/migrations/007-AddDynamicLookups.sql`) applied 2026-10-19 (also tested Up and Down on `Documents_Tests`); only the 7 system categories and 13 system relationships are seeded — no demo data. `ConvertIdsToGuid` (spec 014: every Id / FK → `uniqueidentifier`, system rows get fixed GUIDs from `DomainRules.SystemCategoryId` / `SystemRelationshipId`, sequences dropped; hand-written data-preserving Up, Down throws — back up first; SQL in `../docs/migrations/008-ConvertIdsToGuid.sql`) applied 2026-10-24 to `Documents` (after a verified COPY_ONLY backup `Documents_before_guid_20261024_142703.bak` in the server's default backup folder) and to `Documents_Tests` (140 tests pass). Note: the real DB also has an applied migration `20261020050012_AddRevokedTokensIndex` (extra index `IX_RevokedTokens_Jti`) that is NOT in this code base. `dotnet-ef` is installed as a
global tool (10.x, works with EF 8). `dotnet ef migrations add` is fine. **Ask the user before
`dotnet ef database update` or any SQL write.** Never drop the database.

## Commands (from `backend/`)

```bash
dotnet build Documents.Api.sln
dotnet run --project Documents.Api --launch-profile http    # http://localhost:5000, Swagger at /swagger
```

## Tooling in `.claude/`

- Project agents (written for this repo): `backend-architect`, `backend-coder`,
  `backend-code-reviewer`, `backend-security-auditor`, `backend-tester`,
  `database-migration-specialist`.
- Vendored dotnet-claude-kit: 47 skills, 10 generic agents, `rules/`, `templates/` — see
  `.claude/VENDORED.md`. **Project conventions in this file win** over the kit's preferences
  (Minimal APIs, vertical slices, no repositories, `Result<T>`, file-scoped namespaces).
