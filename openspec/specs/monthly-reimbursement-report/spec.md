# monthly-reimbursement-report Specification

## Purpose
TBD - created by archiving change et012-finance-processing. Update Purpose after archive.
## Requirements
### Requirement: Monthly Reimbursement Data Endpoint
The `Api` layer SHALL expose `GET /api/reports/monthly-reimbursement`, restricted to the
`Finance` role (`docs/SDS.md` §5.6, §8.4), accepting required `year` and `month` query
parameters, and returning the reimbursed-expense data for that month
(`docs/FRS.md` §7.1.4, §10.1.1). In this ticket the endpoint returns a JSON body, not an
`.xlsx` file — confirmed with the ticket owner during `/spec` as the ET012/ET014
boundary: ET014 ("Monthly reimbursement Excel generation using ClosedXML") replaces this
response with the `.xlsx` format described in `docs/SDS.md` §5.6/§8.4, reusing this same
endpoint and underlying query.

#### Scenario: Finance retrieves monthly reimbursement data
- **WHEN** a `Finance` caller GETs `/api/reports/monthly-reimbursement?year=2026&month=7`
- **THEN** the response is `200` with a JSON array of reimbursed-expense records for
  July 2026

#### Scenario: Non-Finance role is rejected
- **WHEN** an authenticated caller whose resolved role is `Employee`, `Manager`, or
  `ComplianceOfficer` GETs `/api/reports/monthly-reimbursement?year=2026&month=7`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `GET /api/reports/monthly-reimbursement` carries no valid
  `Authorization: Bearer` token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

### Requirement: Monthly Reimbursement Data Scope
The report SHALL include only expenses whose `Status` is `Reimbursed` and whose
`ReimbursedAt` (stored in the company local timezone, BR-10) falls within the
calendar month identified by `year`/`month`. Each record SHALL contain the fields listed
in `docs/FRS.md` §10.1.1: Employee (name), Expense Number, Category, Amount, Currency,
Approval Date (`ApprovedAt`, or `ComplianceApprovedAt` for a `ClientEntertainment`
expense — the date the expense became eligible for reimbursement), and Reimbursement
Date (`ReimbursedAt`).

#### Scenario: Only Reimbursed expenses within the month are included
- **WHEN** an expense was reimbursed on 2026-07-15 and another was reimbursed on
  2026-08-02
- **THEN** a request for `year=2026&month=7` includes only the 2026-07-15 expense

#### Scenario: Approval Date reflects Compliance Approval for Client Entertainment
- **WHEN** a `ClientEntertainment` expense was `ComplianceApproved` on 2026-07-10 and
  reimbursed on 2026-07-20
- **THEN** its record's Approval Date is 2026-07-10 (`ComplianceApprovedAt`), not the
  earlier manager `ApprovedAt`

#### Scenario: Non-Reimbursed expenses are excluded regardless of date
- **WHEN** an `Approved` expense's `ApprovedAt` falls within the requested month but it
  has not yet been reimbursed
- **THEN** that expense is excluded from the report

### Requirement: Monthly Reimbursement Query Validation
`GET /api/reports/monthly-reimbursement` SHALL require both `year` and `month`, with
`month` restricted to `1`–`12`. A request missing either parameter, or supplying a
`month` outside `1`–`12`, SHALL be rejected with `400 VALIDATION_ERROR`.

#### Scenario: Missing year is rejected
- **WHEN** a `Finance` caller GETs `/api/reports/monthly-reimbursement?month=7`
- **THEN** the response is `400` with code `VALIDATION_ERROR`

#### Scenario: Missing month is rejected
- **WHEN** a `Finance` caller GETs `/api/reports/monthly-reimbursement?year=2026`
- **THEN** the response is `400` with code `VALIDATION_ERROR`

#### Scenario: Out-of-range month is rejected
- **WHEN** a `Finance` caller GETs `/api/reports/monthly-reimbursement?year=2026&month=13`
- **THEN** the response is `400` with code `VALIDATION_ERROR`

#### Scenario: A month with no reimbursements returns an empty result
- **WHEN** a `Finance` caller GETs `/api/reports/monthly-reimbursement?year=2020&month=1`
  and no expense was reimbursed in that month
- **THEN** the response is `200` with an empty result set, not `404`

