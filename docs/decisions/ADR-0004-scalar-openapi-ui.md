# ADR-0004: Scalar as the Interactive OpenAPI Documentation UI

## Status

Accepted

## Context

`docs/AGENTS.md` §3 and `docs/SDS.md` §2/§11 name "OpenAPI (Swagger)" as the API documentation
tool without specifying a UI product, and ET001 wired up only `Microsoft.AspNetCore.OpenApi`
(`AddOpenApi()` / `MapOpenApi()`) — .NET 9's native OpenAPI document generator. This produces the
raw OpenAPI JSON document at `/openapi/v1.json` in Development but serves **no interactive browsing
UI at all** (no Swashbuckle/Swagger UI package was ever added). `docs/TICKETS.md`'s ET005 scope
explicitly calls for "Scalar OpenAPI UI integration," so this ADR documents adding that UI layer.

## Decision

Add the `Scalar.AspNetCore` NuGet package and call `app.MapScalarApiReference()` immediately after
the existing `app.MapOpenApi()` call, inside the same `IsDevelopment()` block. Scalar renders the
document ASP.NET Core already generates — no change to the OpenAPI document/contract itself, only
the addition of a browsable UI at Scalar's default route (`/scalar/v1`).

Note: earlier ET005 planning documents (proposal.md) described this as "replacing Swagger UI." That
framing was inaccurate — there was no Swagger UI in the codebase to replace. Scalar is purely
additive on top of the pre-existing native OpenAPI document generation.

## Consequences

- A new third-party NuGet dependency (`Scalar.AspNetCore`) is introduced, scoped to `src/Api` and
  Development-only usage — no impact on any runtime business endpoint.
- Developers browsing API docs locally now use `/scalar/v1` instead of the raw JSON document
  directly; no Swagger UI route existed previously, so no route is being removed or redirected.
- `docs/SDS.md` §2/§11's "OpenAPI (Swagger)" wording still describes the underlying document format
  correctly (Swagger = OpenAPI spec); it does not name a specific UI product, so no contradiction
  with the SDS is introduced by adding Scalar as the UI.
