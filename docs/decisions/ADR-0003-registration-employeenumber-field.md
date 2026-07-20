# ADR-0003: Registration Request Field Renamed `employeeId` → `employeeNumber`

## Status

Accepted

## Context

`docs/SDS.md` §5.1 documents the registration request body as `employeeId`, `email`, `password`.
However, the `Employee` entity (`docs/SDS.md` §3.2) has two distinct identifier fields:
`EmployeeId` (Guid primary key, internal/system-generated) and `EmployeeNumber` (string,
human-readable identifier from the CSV seed data). Registration must match the caller-supplied
value against `EmployeeNumber`, not the internal `EmployeeId` Guid — the Guid PK is never exposed
to a registering user, who only knows their employee number.

Naming the wire field `employeeId` while it actually carries an `EmployeeNumber` value invites
confusion for both API consumers and future maintainers, and risks someone wiring it to
`Employee.EmployeeId` by mistake.

## Decision

Rename the registration request field from `employeeId` to `employeeNumber` in the ET004 API
contract, deviating from the literal field name in `docs/SDS.md` §5.1. The field is validated
against `Employee.EmployeeNumber`. `docs/SDS.md` §5.1 should be updated to match in a follow-up
docs change.

## Consequences

- ET004's actual request DTO (`RegisterRequest.EmployeeNumber`) does not literally match
  `docs/SDS.md` §5.1's `employeeId` wording — any reader of the SDS alone would expect a different
  field name until the SDS is updated.
- Frontend (ET016) must use `employeeNumber` as the form field name when this ADR lands, not
  `employeeId`.
- No change to the `Employee.EmployeeId` Guid PK or any other identifier — this ADR is scoped
  solely to the registration request DTO's field name.
