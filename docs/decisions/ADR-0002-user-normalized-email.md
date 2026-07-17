# ADR-0002: User.NormalizedEmail Column

## Status

Accepted

## Context

`docs/SDS.md` §3.3 requires `User.Email` to be "unique (case-insensitive)". SQL Server's default
collation for `nvarchar` columns is already case-insensitive on many instances, but this is
collation-dependent and not guaranteed across every target SQL Server 2022 instance. Relying on
collation for a business-rule-critical uniqueness constraint (anti-account-duplication) is fragile
across environments.

## Decision

Add a `User.NormalizedEmail` column (`Email.ToUpperInvariant()`, populated by whichever ET003/
ET004 service creates or updates a `User`) and put the unique index on `NormalizedEmail` instead
of `Email` directly. This makes the case-insensitive uniqueness guarantee collation-independent and
correct regardless of the target instance's configured collation.

`Employee.Email` is unaffected by this decision and keeps a plain `HasIndex(Email).IsUnique()` —
`docs/SDS.md` §3.2 and `AGENTS.md` §9 describe it as plain "Unique", not case-insensitive-unique,
so it does not need the same treatment.

## Consequences

- `User` gains one field (`NormalizedEmail`) beyond `docs/SDS.md` §3.3's literal list.
- Any service that creates or updates a `User.Email` (ET003 registration, future email-change
  flows if ever added) must also set `NormalizedEmail = Email.ToUpperInvariant()` — a contract this
  ADR establishes for those future tickets to honor.
- Case-insensitive uniqueness of `User.Email` no longer depends on the SQL Server instance's
  collation setting.
