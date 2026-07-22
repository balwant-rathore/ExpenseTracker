# frontend-monthly-report-ui Specification

## Purpose
TBD - created by archiving change et019-dashboard-reports. Update Purpose after archive.
## Requirements
### Requirement: Monthly Report Screen
The frontend SHALL provide a dedicated route (`/reports/monthly-reimbursement`), restricted to the
`Finance` role, offering month and year dropdown pickers pre-selected to the current calendar
month, and a "Download" action that calls
`GET /api/reports/monthly-reimbursement?year={year}&month={month}` for the selected period
(`docs/FRS.md` §7.1.4, §10.1, `monthly-reimbursement-report` capability).

#### Scenario: Finance sees the report screen with the current month pre-selected
- **WHEN** a `Finance` caller navigates to `/reports/monthly-reimbursement`
- **THEN** the frontend SHALL render month and year pickers pre-selected to the current calendar
  month and year, with no report downloaded automatically

#### Scenario: Non-Finance role cannot reach the route
- **WHEN** an authenticated user whose role is not `Finance` navigates to
  `/reports/monthly-reimbursement`
- **THEN** the frontend SHALL NOT render the report screen and SHALL redirect the user away from
  it

### Requirement: Download Action Requests and Saves the Selected Period's Report
Clicking "Download" SHALL call `GET /api/reports/monthly-reimbursement` with the currently
selected `year` and `month`, and SHALL save the returned `.xlsx` response body to disk using the
file name supplied in the response's `Content-Disposition` header
(`monthly-reimbursement-report` capability's file naming requirement).

#### Scenario: Clicking Download requests the selected period
- **WHEN** a `Finance` caller selects year `2026` and month `July`, then clicks "Download"
- **THEN** the frontend SHALL call `GET /api/reports/monthly-reimbursement?year=2026&month=7`

#### Scenario: Downloaded file is saved using the server-supplied file name
- **WHEN** the download request succeeds
- **THEN** the frontend SHALL save the response body as a file named per the response's
  `Content-Disposition` header (e.g. `Monthly-Reimbursement-2026-07.xlsx`), not a client-generated
  name

#### Scenario: Changing the period before downloading again requests the new period
- **WHEN** a `Finance` caller changes the selected month or year after a previous download, then
  clicks "Download" again
- **THEN** the frontend SHALL call `GET /api/reports/monthly-reimbursement` with the newly
  selected `year`/`month`, not the previous selection

#### Scenario: A month with no reimbursements still downloads a valid empty workbook
- **WHEN** the selected period has no reimbursed expenses and the backend returns `200` with a
  header-only workbook
- **THEN** the frontend SHALL save that workbook file exactly as it would a non-empty one, with
  no error shown

#### Scenario: Backend validation error is surfaced without attempting a save
- **WHEN** `GET /api/reports/monthly-reimbursement` responds `400 VALIDATION_ERROR`
- **THEN** the frontend SHALL display the returned error and SHALL NOT attempt to save a file

