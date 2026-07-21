## MODIFIED Requirements

### Requirement: Single Expense Retrieval Endpoint
The `Api` layer SHALL expose `GET /api/expenses/{id}`, returning the expense mapped to
`ExpenseResponse` (`docs/FRS.md` §4.4, `docs/SDS.md` §5.2) when the authenticated caller
is permitted to view it under the same per-role default visibility rules as
`GET /api/expenses` (the `expense-visibility` capability):
- **Employee** — only their own expense, any `Status`.
- **Manager** — their own expense (any `Status`), or a direct report's expense whose
  `Status` is not `Draft`.
- **Finance** — any expense whose `Status` is not `Draft`.
- **ComplianceOfficer** — a `ClientEntertainment`-category expense whose `Status` is
  `Approved` or `ComplianceApproved`.

A caller requesting an expense that exists but falls outside their visible set
receives `403 AUTHORIZATION_FAILED`, matching ET008's established non-owner-caller
precedent (confirmed with the ticket owner during `/spec` as an intentional
anti-enumeration tradeoff, not a defect).

#### Scenario: Owner retrieves their own expense
- **WHEN** the owning employee GETs `/api/expenses/{id}` for their own expense
- **THEN** the response is `200` with the expense's current data

#### Scenario: Non-owner is rejected
- **WHEN** an `Employee`-role caller who does not own the expense, and is not its
  owner's manager/Finance/an eligible Compliance reviewer, GETs `/api/expenses/{id}`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED` (unchanged from
  ET008 — this is the Employee-role instance of the broader per-role visibility rule
  below; the Manager/Finance/Compliance instances of the same rule are covered by the
  scenarios that follow)

#### Scenario: Manager retrieves their own expense
- **WHEN** a `Manager` GETs `/api/expenses/{id}` for an expense they own, regardless of
  its `Status`
- **THEN** the response is `200` with the expense's current data

#### Scenario: Manager retrieves a direct report's non-Draft expense
- **WHEN** a `Manager` GETs `/api/expenses/{id}` for a direct report's expense whose
  `Status` is not `Draft`
- **THEN** the response is `200` with the expense's current data

#### Scenario: Manager is rejected for a direct report's Draft expense
- **WHEN** a `Manager` GETs `/api/expenses/{id}` for a direct report's `Draft` expense
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Manager is rejected for an unrelated employee's expense
- **WHEN** a `Manager` GETs `/api/expenses/{id}` for an expense belonging to an
  employee who is neither them nor their direct report
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Finance retrieves any non-Draft expense
- **WHEN** a `Finance` caller GETs `/api/expenses/{id}` for any employee's expense
  whose `Status` is not `Draft`
- **THEN** the response is `200` with the expense's current data

#### Scenario: Finance is rejected for a Draft expense
- **WHEN** a `Finance` caller GETs `/api/expenses/{id}` for an expense whose `Status`
  is `Draft`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Compliance retrieves an Approved or Compliance Approved Client Entertainment expense
- **WHEN** a `ComplianceOfficer` GETs `/api/expenses/{id}` for a `ClientEntertainment`
  expense whose `Status` is `Approved` or `ComplianceApproved`
- **THEN** the response is `200` with the expense's current data

#### Scenario: Compliance is rejected for a Reimbursed Client Entertainment expense
- **WHEN** a `ComplianceOfficer` GETs `/api/expenses/{id}` for a `ClientEntertainment`
  expense whose `Status` is `Reimbursed`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Compliance is rejected for a non-Client-Entertainment expense
- **WHEN** a `ComplianceOfficer` GETs `/api/expenses/{id}` for an expense in any other
  category
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Nonexistent expense is rejected
- **WHEN** `GET /api/expenses/{id}` targets an id with no matching `Expense` row
- **THEN** the response is `404` with code `RESOURCE_NOT_FOUND`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `GET /api/expenses/{id}` carries no valid `Authorization: Bearer`
  token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`
