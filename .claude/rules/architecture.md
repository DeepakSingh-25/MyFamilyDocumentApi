---
alwaysApply: true
description: >
  Dependency direction, layer placement and endpoint organization for the My Family Documents API
  (Clean Architecture with MediatR + repositories + MVC controllers).
---

# Architecture Rules (My Family Documents API)

> Adapted for this repo. The upstream dotnet-claude-kit version of this file recommends Minimal API
> endpoint modules, feature folders and no repository pattern; this project deliberately uses
> controllers + MediatR + repositories (same as the reference API). Follow that shape. Propose a
> migration only when the user asks for one.

## Layers and dependency direction

```
Documents.Core            entities, repository interfaces, DTO models, options   (references nothing)
Documents.Application     MediatR commands / queries / events + handlers         -> Core
Documents.Infrastructure  AppDbContext, repositories, file storage, JWT, BCrypt  -> Application
Documents.Api             controllers, Program.cs, Swagger, CORS                 -> Application, Infrastructure
```

- **Never reverse an arrow.** Core must not reference EF Core, ASP.NET or MediatR. Application must
  not reference EF Core or ASP.NET.
- **EF Core and the file system only in Infrastructure.** Handlers talk to `I*Repository` /
  `I*Storage` interfaces from `Documents.Core/Interfaces`.

## Data access

- One repository interface + implementation per aggregate (`IFamilyMemberRepository` /
  `FamilyMemberRepository`, `IDocumentRepository` / `DocumentRepository`). No generic `IRepository<T>`.
- **Every member/document repository method takes the caller's `userId` and filters by it.**
- Repositories own `SaveChangesAsync`. Use `AsNoTracking()` for read-only queries and pass the
  `CancellationToken` through.
- Register repositories `AddScoped` in `Documents.Infrastructure/DependencyInjection.cs`.

## Use cases

- One file per use case in `Documents.Application/Commands|Queries`: the `record ... : IRequest<T>`
  and its `...Handler` live together. Domain events are `INotification` records in `Events/` with one
  handler per file (`TaskWhenAllPublisher` runs them in parallel).

## Endpoints

- MVC controllers in `Documents.Api/Controllers`, `[Route("api/[controller]")]` + `[ApiController]`,
  primary-constructor `ISender`. An action does one `sender.Send(...)` and maps the result to an HTTP
  status — no business logic, no `DbContext` in controllers.
- Mark every controller `[Authorize]` or `[AllowAnonymous]` explicitly.
- JSON casing: see the open decision in `../CLAUDE.md`; don't change it in a feature change.

## Shared code

- Request/response DTOs go in `Documents.Core/Models`; options classes in `Documents.Core/Options`.
  Business rules belong in handlers or entities, not in DTOs or static helpers.
