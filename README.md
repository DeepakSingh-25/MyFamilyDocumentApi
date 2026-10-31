# My Family Documents — API (.NET 10)

Blank Clean Architecture skeleton. Layout copied from the reference API in `D:\Working\work\API`
(Core ← Application ← Infrastructure ← Api, MediatR, EF Core on SQL Server, JWT).

| Project | Holds | References |
|---|---|---|
| `Documents.Core` | `Entities/`, `Interfaces/` (repository ports), `Models/` (request/response DTOs), `Options/` | — |
| `Documents.Application` | `Commands/`, `Queries/`, `Events/` — MediatR request + handler in the same file | Core |
| `Documents.Infrastructure` | `Data/AppDbContext.cs`, `Repositories/`, `Services/`, `Authorization/`, `Migrations/`, JWT setup | Application |
| `Documents.Api` | `Controllers/`, `Program.cs`, Swagger, CORS | Application, Infrastructure |

Each layer has a `DependencyInjection.cs` (`AddCoreDI`, `AddApplicationDI`, `AddInfrastructureDI`),
chained by `Documents.Api/DependencyInjection.cs` → `AddAppDI`.

## Run

```bash
dotnet run --project Documents.Api --launch-profile http
```

- API: http://localhost:5000 — Swagger at `/swagger`
- CORS allows the Angular app on `localhost:5500`
- JSON is camelCase; errors are ProblemDetails (message in `detail`)

## Configuration

- `ConnectionStrings:DefaultConnection` → in user-secrets (server 192.168.120.17, database `Documents`, `InitialCreate` applied)
- `Signup:SetupCode` → in user-secrets; one-time code for the first (and only) account
- `Jwt:SecretKey` is **not** in `appsettings.json`. On this machine it is in user-secrets.
  On a new machine set it once:

```bash
dotnet user-secrets set "Jwt:SecretKey" "<random 32+ character string>" --project Documents.Api
```

## Adding a feature (same path as the reference)

1. Entity → `Core/Entities/<Name>Entity.cs`, add a `DbSet` in `AppDbContext`
2. Port → `Core/Interfaces/I<Name>Repository.cs`
3. Adapter → `Infrastructure/Repositories/<Name>Repository.cs`, register in `Infrastructure/DependencyInjection.cs`
4. Use cases → `Application/Commands/...Command.cs`, `Application/Queries/...Query.cs`
5. Endpoint → `Api/Controllers/<Name>Controller.cs` with `ISender`
6. Migration → `dotnet ef migrations add <Name> --project Documents.Infrastructure --startup-project Documents.Api`
