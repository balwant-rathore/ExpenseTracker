# frontend-expense-review-actions-ui Specification

## Purpose
TBD - created by archiving change et018-expense-review. Update Purpose after archive.
## Requirements
### Requirement: Manager Approve and Reject Actions
The frontend SHALL show "Approve" and "Reject" actions on the expense detail screen when the
authenticated caller's role is `Manager`, the viewed expense's `Status` is `Submitted`, and the
caller is not its owner (`manager-expense-review` capability — the direct-manager/skip-level
authorization check is enforced authoritatively server-side; the frontend need only avoid
rendering the actions in cases the backend would reject, not duplicate the hierarchy check).
Approve SHALL call `POST /api/expenses/{id}/approve` directly. Reject SHALL open the shared
rejection-comment dialog (see the Shared Rejection Comment Dialog requirement below) and call
`POST /api/expenses/{id}/reject` with the entered comment. Both SHALL invalidate and refetch the
expense on success and surface the returned error otherwise, without showing a false-success state
(`docs/FRS.md` §5.1).

#### Scenario: Manager sees Approve/Reject on a report's Submitted expense
- **WHEN** a `Manager` views the detail of a `Submitted` expense owned by one of their direct
  reports
- **THEN** the frontend SHALL show the "Approve" and "Reject" actions

#### Scenario: Manager does not see Approve/Reject on their own expense
- **WHEN** a `Manager` views the detail of their own `Submitted` expense
- **THEN** the frontend SHALL NOT show the "Approve" or "Reject" action (BR-06)

#### Scenario: Manager does not see Approve/Reject on a non-Submitted expense
- **WHEN** a `Manager` views the detail of a direct report's expense whose `Status` is not
  `Submitted` (e.g. `Approved`)
- **THEN** the frontend SHALL NOT show the "Approve" or "Reject" action

#### Scenario: Approve calls the approve endpoint and refetches on success
- **WHEN** a `Manager` clicks "Approve" on an eligible expense
- **THEN** the frontend SHALL call `POST /api/expenses/{id}/approve`, and on success SHALL
  invalidate and refetch the expense so its `Status` reflects `Approved`

#### Scenario: Reject requires the comment dialog before calling the reject endpoint
- **WHEN** a `Manager` clicks "Reject" on an eligible expense
- **THEN** the frontend SHALL open the shared rejection-comment dialog and SHALL NOT call
  `POST /api/expenses/{id}/reject` until a valid comment is submitted

#### Scenario: Backend rejection is surfaced without a false-success state
- **WHEN** `POST /api/expenses/{id}/approve` or `POST /api/expenses/{id}/reject` responds `403` or
  `422`
- **THEN** the frontend SHALL display the returned error and SHALL NOT show the action as
  successful or change the displayed `Status`

### Requirement: Compliance Approve and Reject Actions
The frontend SHALL show "Approve" and "Reject" actions on the expense detail screen when the
authenticated caller's role is `ComplianceOfficer`, the viewed expense's `Category` is
`ClientEntertainment`, and its `Status` is `Approved` — no ownership/hierarchy restriction applies
(`compliance-expense-review` capability). Approve SHALL call
`POST /api/expenses/{id}/compliance-approve` directly. Reject SHALL open the shared
rejection-comment dialog and call `POST /api/expenses/{id}/compliance-reject` with the entered
comment. Both SHALL invalidate and refetch the expense on success and surface the returned error
otherwise (`docs/FRS.md` §5.2).

#### Scenario: Compliance sees actions on an Approved Client Entertainment expense
- **WHEN** a `ComplianceOfficer` views the detail of an `Approved` `ClientEntertainment` expense
- **THEN** the frontend SHALL show the "Approve" and "Reject" actions

#### Scenario: Compliance does not see actions on a non-Client-Entertainment expense
- **WHEN** a `ComplianceOfficer` views the detail of an `Approved` expense in any other category
- **THEN** the frontend SHALL NOT show the "Approve" or "Reject" action

#### Scenario: Compliance does not see actions on a non-Approved Client Entertainment expense
- **WHEN** a `ComplianceOfficer` views the detail of a `ClientEntertainment` expense whose `Status`
  is not `Approved` (e.g. `Submitted` or `ComplianceApproved`)
- **THEN** the frontend SHALL NOT show the "Approve" or "Reject" action

#### Scenario: Approve calls the compliance-approve endpoint and refetches on success
- **WHEN** a `ComplianceOfficer` clicks "Approve" on an eligible expense
- **THEN** the frontend SHALL call `POST /api/expenses/{id}/compliance-approve`, and on success
  SHALL invalidate and refetch the expense so its `Status` reflects `ComplianceApproved`

#### Scenario: Reject requires the comment dialog before calling the compliance-reject endpoint
- **WHEN** a `ComplianceOfficer` clicks "Reject" on an eligible expense
- **THEN** the frontend SHALL open the shared rejection-comment dialog and SHALL NOT call
  `POST /api/expenses/{id}/compliance-reject` until a valid comment is submitted

#### Scenario: Backend rejection is surfaced without a false-success state
- **WHEN** `POST /api/expenses/{id}/compliance-approve` or
  `POST /api/expenses/{id}/compliance-reject` responds `403` or `422`
- **THEN** the frontend SHALL display the returned error and SHALL NOT show the action as
  successful or change the displayed `Status`

### Requirement: Finance Reimburse Action
The frontend SHALL show a "Reimburse" action on the expense detail screen when the authenticated
caller's role is `Finance` and the viewed expense satisfies the category-conditioned
reimbursement precondition — `Status: Approved` for any non-`ClientEntertainment` category, or
`Status: ComplianceApproved` for `ClientEntertainment` — with no ownership restriction
(`expense-reimbursement` capability). Clicking it SHALL call `POST /api/expenses/{id}/reimburse`
directly (no comment required), and SHALL invalidate and refetch the expense on success
(`docs/FRS.md` §7.1.3).

#### Scenario: Finance sees Reimburse on an eligible non-Client-Entertainment expense
- **WHEN** a `Finance` caller views the detail of an `Approved` `Travel` expense
- **THEN** the frontend SHALL show the "Reimburse" action

#### Scenario: Finance sees Reimburse on an eligible Client Entertainment expense
- **WHEN** a `Finance` caller views the detail of a `ComplianceApproved` `ClientEntertainment`
  expense
- **THEN** the frontend SHALL show the "Reimburse" action

#### Scenario: Finance does not see Reimburse on an ineligible status
- **WHEN** a `Finance` caller views the detail of a `ClientEntertainment` expense whose `Status`
  is `Approved` (not yet Compliance Approved)
- **THEN** the frontend SHALL NOT show the "Reimburse" action

#### Scenario: Reimburse calls the endpoint and refetches on success
- **WHEN** a `Finance` caller clicks "Reimburse" on an eligible expense
- **THEN** the frontend SHALL call `POST /api/expenses/{id}/reimburse`, and on success SHALL
  invalidate and refetch the expense so its `Status` reflects `Reimbursed`

#### Scenario: Backend rejection is surfaced without a false-success state
- **WHEN** `POST /api/expenses/{id}/reimburse` responds `422`
- **THEN** the frontend SHALL display the returned error and SHALL NOT show the action as
  successful or change the displayed `Status`

### Requirement: Shared Rejection Comment Dialog
Manager Reject and Compliance Reject SHALL use one shared dialog component (mirroring the
existing `CancelExpenseDialog` confirm-dialog pattern) rather than two separately implemented
components, since both endpoints enforce an identical mandatory-comment rule (present, non-empty
after trimming, ≤500 characters — `manager-expense-review` and `compliance-expense-review`
capabilities). The dialog SHALL disable submission until the entered comment satisfies that rule
(UX-only client check; the backend remains authoritative) and SHALL call whichever endpoint
(`reject` or `compliance-reject`) the caller invoked it from.

#### Scenario: Empty comment disables submission
- **WHEN** the rejection dialog is open and the comment field is empty
- **THEN** the submit control SHALL be disabled

#### Scenario: Whitespace-only comment disables submission
- **WHEN** the rejection dialog is open and the comment field contains only whitespace
- **THEN** the submit control SHALL be disabled

#### Scenario: Valid comment from a Manager calls the reject endpoint
- **WHEN** a `Manager` enters a valid comment and submits the rejection dialog opened from a
  `Submitted` expense's detail
- **THEN** the frontend SHALL call `POST /api/expenses/{id}/reject` with that comment

#### Scenario: Valid comment from Compliance calls the compliance-reject endpoint
- **WHEN** a `ComplianceOfficer` enters a valid comment and submits the rejection dialog opened
  from an `Approved` `ClientEntertainment` expense's detail
- **THEN** the frontend SHALL call `POST /api/expenses/{id}/compliance-reject` with that comment

