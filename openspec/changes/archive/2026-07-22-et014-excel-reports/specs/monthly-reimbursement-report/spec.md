## MODIFIED Requirements

### Requirement: Monthly Reimbursement Data Endpoint
The `Api` layer SHALL expose `GET /api/reports/monthly-reimbursement`, restricted to the
`Finance` role (`docs/SDS.md` §5.6, §8.4), accepting required `year` and `month` query
parameters, and returning the reimbursed-expense data for that month as a downloadable
Microsoft Excel (`.xlsx`) file (`docs/FRS.md` §7.1.4, §10.1.1, §10.1.2; `docs/SDS.md`
§5.6, §8.4). This replaces the JSON response the endpoint returned under ET012, per the
ET012/ET014 boundary recorded when this capability's spec was first created ("ET014 ...
replaces this response with the `.xlsx` format ... reusing this same endpoint and
underlying query").

#### Scenario: Finance retrieves monthly reimbursement data
- **WHEN** a `Finance` caller GETs `/api/reports/monthly-reimbursement?year=2026&month=7`
- **THEN** the response is `200` with `Content-Type:
  application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, a
  `Content-Disposition: attachment` header naming the file, and a body that is a valid
  `.xlsx` workbook containing the reimbursed-expense records for July 2026

#### Scenario: Non-Finance role is rejected
- **WHEN** an authenticated caller whose resolved role is `Employee`, `Manager`, or
  `ComplianceOfficer` GETs `/api/reports/monthly-reimbursement?year=2026&month=7`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `GET /api/reports/monthly-reimbursement` carries no valid
  `Authorization: Bearer` token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

## ADDED Requirements

### Requirement: Monthly Reimbursement Workbook File Naming
The generated `.xlsx` file SHALL be named `Monthly-Reimbursement-{yyyy}-{MM}.xlsx`, where
`{yyyy}` is the requested 4-digit year and `{MM}` is the requested month zero-padded to two
digits (`docs/SDS.md` §8.4).

#### Scenario: Single-digit month is zero-padded in the file name
- **WHEN** a `Finance` caller GETs `/api/reports/monthly-reimbursement?year=2026&month=7`
- **THEN** the response's `Content-Disposition` header names the file
  `Monthly-Reimbursement-2026-07.xlsx`

### Requirement: Monthly Reimbursement Workbook Layout
The workbook SHALL contain a single header row with columns, in order: Employee, Expense
Number, Category, Amount, Currency, Approval Date, Reimbursement Date (`docs/FRS.md`
§10.1.1), followed by one data row per reimbursed expense matching the data scope and
Approval Date rules defined in the "Monthly Reimbursement Data Scope" requirement of this
capability. Approval Date and Reimbursement Date cells SHALL be written as Excel date
values formatted `yyyy-MM-dd`.

#### Scenario: Header row matches FRS field order
- **WHEN** a `Finance` caller downloads the workbook for a month
- **THEN** row 1 contains the headers Employee, Expense Number, Category, Amount,
  Currency, Approval Date, Reimbursement Date, in that order

#### Scenario: Date cells use Excel date formatting
- **WHEN** a reimbursed expense's Approval Date is 2026-07-10 and Reimbursement Date is
  2026-07-20
- **THEN** the corresponding cells in the workbook are Excel date values formatted
  `yyyy-MM-dd`

### Requirement: Empty Monthly Reimbursement Workbook
A month with no reimbursed expenses SHALL still return `200 OK` with a valid `.xlsx`
workbook containing only the header row and no data rows, not `404`.

#### Scenario: A month with no reimbursements returns a header-only workbook
- **WHEN** a `Finance` caller GETs `/api/reports/monthly-reimbursement?year=2020&month=1`
  and no expense was reimbursed in that month
- **THEN** the response is `200` with an `.xlsx` workbook containing only the header row
  and no data rows
