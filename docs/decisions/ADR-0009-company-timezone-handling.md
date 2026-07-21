# ADR-0009: Company-Local Timezone for Expense Date Comparisons

## Status

Accepted

## Context

A `/review` of ET007 flagged that `ExpenseService`'s BR-02 ("Expense Date cannot be in the
future") check and `ExpenseNumberGenerator`'s `yyyyMMdd` date prefix both compared against
`DateTime.UtcNow`, not the "company's local timezone" `docs/FRS.md` BR-10 requires. Near a UTC/
local day boundary, this could wrongly accept or reject a Draft/Submit, or generate an expense
number dated the wrong calendar day.

This UTC-everywhere pattern is systemic across the whole backend (`AuthService`,
`RefreshTokenService`, `AttachmentService` all timestamp with `DateTime.UtcNow`) — but ET007 is
the first ticket to implement business logic that actually branches on "which calendar day is it
today," so it's the first place the gap has real behavioral consequences. Rewriting every
`DateTime.UtcNow` call in the codebase to store local time is a much larger, separate concern
(instant columns like `CreatedAt`/`SubmittedAt` are arguably *better* stored as UTC instants
regardless of BR-10 — storing local wall-clock time directly is a well-known footgun around DST
transitions). This ADR scopes the fix to the specific drift identified: calendar-day comparisons
and the expense-number date basis.

## Decision

Add `ICompanyClock` (`backend/src/Application/Expenses/`), backed by `CompanyClock`, which
converts `DateTime.UtcNow` to the configured company timezone (`CompanyTimeZone:TimeZoneId` in
`appsettings.json`, defaulting to `Asia/Kolkata` — the IANA identifier, which `TimeZoneInfo`
resolves cross-platform on .NET's ICU-backed implementation, unlike Windows-only IDs such as
"India Standard Time") and exposes `Today()` as a `DateOnly`. `ExpenseService` (both `CreateAsync`
and `SubmitAsync`) and `ExpenseNumberGenerator` use `ICompanyClock.Today()` instead of
`DateOnly.FromDateTime(DateTime.UtcNow)`/`DateTime.UtcNow:yyyyMMdd` for their calendar-day logic.

`CreatedAt`/`UpdatedAt`/`SubmittedAt`/etc. remain UTC instants — out of scope for this fix, since
they aren't calendar-day comparisons and rewriting them is the larger, separate, cross-cutting
concern noted above.

Rejected alternative: leave the gap and treat it as accepted technical debt. Rejected because the
`/review` finding is a genuine BR-02/BR-10 compliance gap in newly-written code, not just
pre-existing debt being carried forward — it was practical to fix at the scope of "the two
places ET007 added calendar-day logic" without taking on the full-codebase rewrite.

## Consequences

- `ExpenseService`/`ExpenseNumberGenerator` gain a constructor dependency on `ICompanyClock`,
  injected via DI (`ExpenseServiceCollectionExtensions`).
- Tests for both now take a fake `ICompanyClock` instead of relying on wall-clock
  `DateTime.UtcNow` at test-run time — a secondary benefit: date-boundary tests become
  deterministic rather than time-of-run-dependent.
- Every *other* `DateTime.UtcNow` timestamp in the codebase (audit fields, token expiry, etc.)
  is unaffected and remains a documented, tracked follow-up (see `/review` output and
  `docs/TICKETS.md`), not silently expanded into this fix.
