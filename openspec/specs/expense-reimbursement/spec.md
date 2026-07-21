# expense-reimbursement Specification

## Purpose
TBD - created by archiving change et012-finance-processing. Update Purpose after archive.
## Requirements
### Requirement: Reimbursement Endpoint
The `Api` layer SHALL expose `POST /api/expenses/{id}/reimburse`, restricted to the
`Finance` role (`docs/SDS.md` §5.2, §6.4), transitioning an eligible expense (per the
Category-Conditioned Precondition requirement below) to `Reimbursed`
(`docs/FRS.md` §7.1.3, BR-08). No ownership/hierarchy check is performed — any
authenticated `Finance` caller may reimburse any eligible expense, since the SDS §6.4
authorization matrix grants `Finance` organization-wide reimbursement authority with no
per-employee scoping.

#### Scenario: Finance reimburses an eligible expense
- **WHEN** a `Finance` caller POSTs to `/api/expenses/{id}/reimburse` for an expense
  that satisfies the Category-Conditioned Precondition
- **THEN** the response is `200` and the expense's `Status` becomes `Reimbursed`

#### Scenario: Non-Finance role is rejected
- **WHEN** an authenticated caller whose resolved role is `Employee`, `Manager`, or
  `ComplianceOfficer` POSTs to `/api/expenses/{id}/reimburse`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Nonexistent expense is rejected
- **WHEN** `POST /api/expenses/{id}/reimburse` targets an id with no matching `Expense`
  row
- **THEN** the response is `404` with code `RESOURCE_NOT_FOUND`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `POST /api/expenses/{id}/reimburse` carries no valid
  `Authorization: Bearer` token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

### Requirement: Category-Conditioned Reimbursement Precondition
`POST /api/expenses/{id}/reimburse` SHALL require:
- a `ClientEntertainment` expense's `Status` to be `ComplianceApproved`;
- any other category's `Status` to be `Approved`.

A request targeting an expense that does not satisfy the rule for its own category
SHALL be rejected with `422 BUSINESS_RULE_VIOLATION`, and `Status` left unchanged. This
reconciles the literal text of `docs/FRS.md` §7.1.3 ("Approved or Compliance Approved")
with the two distinct workflow paths in `docs/SDS.md` §6.1/§6.3 — a `ClientEntertainment`
expense must complete Compliance Officer review before Finance can reimburse it, so a
`ClientEntertainment` expense still at `Approved` is not yet eligible. Confirmed with
the ticket owner during `/spec` and logged as an ADR in `docs/decisions/`.

#### Scenario: Reimbursing an Approved non-Client-Entertainment expense succeeds
- **WHEN** `POST /api/expenses/{id}/reimburse` targets a `Travel` expense whose `Status`
  is `Approved`
- **THEN** the response is `200` and the expense's `Status` becomes `Reimbursed`

#### Scenario: Reimbursing a ComplianceApproved Client Entertainment expense succeeds
- **WHEN** `POST /api/expenses/{id}/reimburse` targets a `ClientEntertainment` expense
  whose `Status` is `ComplianceApproved`
- **THEN** the response is `200` and the expense's `Status` becomes `Reimbursed`

#### Scenario: Reimbursing an Approved (not yet Compliance Approved) Client Entertainment expense is rejected
- **WHEN** `POST /api/expenses/{id}/reimburse` targets a `ClientEntertainment` expense
  whose `Status` is `Approved`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status`
  remains `Approved`

#### Scenario: Reimbursing a ComplianceApproved non-Client-Entertainment expense is rejected
- **WHEN** `POST /api/expenses/{id}/reimburse` targets a `Meals` expense whose `Status`
  is `ComplianceApproved` (an inconsistent state that should not normally occur)
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

### Requirement: Non-Eligible Statuses Are Rejected on Reimburse
`POST /api/expenses/{id}/reimburse` SHALL reject, with `422 BUSINESS_RULE_VIOLATION` and
the `Status` left unchanged, a request targeting an expense whose `Status` is `Draft`,
`Submitted`, `Rejected`, `Cancelled`, or already `Reimbursed` (no backward transitions;
no re-reimbursing a terminal expense — `docs/FRS.md` §7.2.4).

#### Scenario: Reimbursing a Submitted expense is rejected
- **WHEN** `POST /api/expenses/{id}/reimburse` targets an expense whose `Status` is
  `Submitted`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

#### Scenario: Reimbursing a Rejected expense is rejected
- **WHEN** `POST /api/expenses/{id}/reimburse` targets an expense whose `Status` is
  `Rejected`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status`
  remains `Rejected`

#### Scenario: Reimbursing an already-Reimbursed expense is rejected
- **WHEN** `POST /api/expenses/{id}/reimburse` targets an expense whose `Status` is
  already `Reimbursed`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status`
  remains `Reimbursed`

#### Scenario: Reimbursing a Cancelled expense is rejected
- **WHEN** `POST /api/expenses/{id}/reimburse` targets an expense whose `Status` is
  `Cancelled`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status`
  remains `Cancelled`

### Requirement: Audit Field Population on Reimburse
On a successful `POST /api/expenses/{id}/reimburse`, the system SHALL set
`ReimbursedAt` to the current timestamp and `ReimbursedByEmployeeId` to the caller's
`EmployeeId` (`docs/SDS.md` §6.8). The endpoint SHALL NOT accept or honor a
client-supplied value for either field.

#### Scenario: Reimbursement populates ReimbursedAt and ReimbursedByEmployeeId
- **WHEN** an eligible expense is successfully reimbursed
- **THEN** `ReimbursedAt` is set to the current timestamp and `ReimbursedByEmployeeId`
  is set to the reimbursing Finance employee's `EmployeeId`

#### Scenario: Client cannot set audit fields directly
- **WHEN** a `POST /api/expenses/{id}/reimburse` request body includes
  `reimbursedAt` or `reimbursedByEmployeeId`
- **THEN** those values are ignored, and the system-computed values are used instead

