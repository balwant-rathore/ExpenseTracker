# ADR-0016: Company-Local Timezone for Monthly Reimbursement Report Month Boundaries

## Status

Accepted

## Context

A `/review` of ET014 (monthly reimbursement Excel export) flagged that `ReportService
.GetMonthlyReimbursementAsync`'s month-range filter computed `rangeStart`/`rangeEnd` directly as
`DateTimeKind.Utc` boundaries of the requested calendar month, then compared them against
`Expense.ReimbursedAt` (an already-UTC instant). Near a UTC/company-local day boundary — Asia/
Kolkata is UTC+5:30 — this misclassifies which calendar month a reimbursement belongs to: e.g. a
`ReimbursedAt` of `2026-06-30T19:00:00Z` is `2026-07-01T00:30` in company-local time (already
July) but would have been excluded from the July report under a naive UTC range, and conversely
an instant just after UTC midnight on the 1st can be wrongly included in the *previous* local
month's report. This is the same class of bug ADR-0009 fixed for BR-02's day-boundary check, now
surfacing at report *month* boundaries instead of a single day boundary.

## Decision

Extend `ICompanyClock` (`backend/src/Application/Expenses/`) — already the single source of
truth for the configured company IANA timezone (`CompanyTimeZone:TimeZoneId`) — with
`DateTime ConvertLocalToUtc(DateTime localDateTime)`. `ReportService` now computes the report's
month range as the *company-local* calendar month's start/end, converted to UTC via this method,
before querying `IExpenseRepository.GetReimbursedForReportAsync`. This reuses the existing
timezone configuration and `TimeZoneInfo` resolution rather than introducing a second,
independent conversion path.

Rejected alternative: add a report-specific timezone helper scoped to `Application.Reports`.
Rejected because it would duplicate the `TimeZoneInfo.FindSystemTimeZoneById` resolution logic
`CompanyClock` already owns, risking the two implementations drifting if the configured timezone
ever changes.

## Consequences

- `ReportService` gains a constructor dependency on `ICompanyClock`. No new DI registration was
  needed — `ICompanyClock` is already registered by `AddExpenseFoundation`, called alongside
  `AddReportFoundation` in `Program.cs`.
- `FakeCompanyClock` (test double, `tests/UnitTests/Application/Expenses/ExpenseNumberGeneratorTests.cs`)
  gained a `ConvertLocalToUtc` implementation and a settable `TimeZone` property, defaulting to
  `Asia/Kolkata` to match production's default.
- Two regression tests were added to `ReportServiceTests` proving a `ReimbursedAt` just after
  local midnight is now correctly included in / excluded from the requested month, where the old
  UTC-naive logic got both cases wrong.
- Every *other* `DateTime.UtcNow` audit-instant field (`CreatedAt`, `SubmittedAt`, `ApprovedAt`,
  etc.) remains an untouched UTC instant, per ADR-0009 — this ADR only extends the
  calendar-boundary-comparison scope ADR-0009 established; it does not revisit that decision.
