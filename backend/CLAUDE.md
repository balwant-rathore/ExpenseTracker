@../AGENTS.md

Backend-specific rules for `backend/`. Stack, schema, and workflow details are in the root
`AGENTS.md` — this file covers only how to work inside this project.

## Commands (run from `backend/`)

```bash
dotnet build                          # compile, surfaces analyzer warnings
dotnet run --project src/Api          # run API locally
dotnet test                           # all tests
dotnet test --filter FullyQualifiedName~UnitTests        # unit only
dotnet test --filter FullyQualifiedName~IntegrationTests # integration only
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api
dotnet ef database update --project src/Infrastructure --startup-project src/Api
dotnet format                         # apply EditorConfig formatting
```

## Framework Patterns

- New endpoint = thin controller action → `Application` service method → returns a DTO, never an
  EF entity directly.
- Business rules and workflow transitions live in `Application` services, not in controllers or
  EF entity classes.
- Register services in DI as interfaces (`IExpenseService` → `ExpenseService`); controllers and
  services depend on the interface, never the concrete type.
- Validation: FluentValidation (or DataAnnotations, whichever the project adopts first — stay
  consistent) runs before any service method touches the database.
- All EF Core queries for list/search endpoints apply filtering, sorting, and paging in the query
  itself (`IQueryable` composition) — never materialize a full table then filter in memory.
- Wrap multi-step state changes (e.g. approve → set audit fields → persist) in a single
  `DbContext` SaveChanges transaction; enqueue the notification only after that commit succeeds.

## Anti-Patterns to Avoid

- Don't return `Domain` entities from controllers — always map to an `Application`-layer DTO.
- Don't put `if (role == ...)` authorization checks inline in controllers — use policy-based
  authorization (`[Authorize(Policy = ...)]`) so rules live in one place.
- Don't call `SaveChanges` multiple times across one logical workflow transition.
- Don't inject `DbContext` directly into controllers — go through a service/repository.
- Don't use `.Result`/`.Wait()` on async calls; don't leave a method `async` without actually
  awaiting anything inside it.
- Don't hand-write SQL migrations — always generate via `dotnet ef migrations add`.
- Don't catch exceptions in a controller/service just to swallow or rethrow generically — let the
  global exception handler do its job.
