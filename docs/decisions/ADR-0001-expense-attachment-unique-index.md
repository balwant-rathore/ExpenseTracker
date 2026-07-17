# ADR-0001: Unique Index on Expense.AttachmentId

## Status

Accepted

## Context

`docs/SDS.md` §3.1 shows a strict one-to-one relationship between `Expense` and `Attachment`
("Expense └── 1 ── 1 Attachment"). However, the index list in §3.6 for the `Expense` entity does
not include an index on `AttachmentId`, so nothing in the literal schema prevents two different
`Expense` rows from referencing the same `Attachment`, which would violate the diagrammed 1:1
cardinality (FRS §14 assumption: "Each expense has only one receipt attachment").

## Decision

Add a unique index on `Expense.AttachmentId` in the `ExpenseConfiguration` EF Core configuration,
in addition to the indexes explicitly listed in `docs/SDS.md` §3.6. This enforces at the database
level that an `Attachment` can back at most one `Expense`.

## Consequences

- A second `Expense` insert referencing an already-referenced `AttachmentId` fails with a unique
  constraint violation, matching the "An Attachment cannot back two Expenses" scenario in
  `openspec/changes/et002-domain-setup/specs/domain-model/spec.md`.
- This is additive to the literal `docs/SDS.md` §3.6 index list, not a contradiction of it — no
  other schema behavior changes.
