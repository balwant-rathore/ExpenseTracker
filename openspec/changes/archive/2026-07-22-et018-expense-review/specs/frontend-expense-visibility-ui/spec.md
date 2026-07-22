## MODIFIED Requirements

### Requirement: Action Visibility on Detail Reflects Ownership and Status
The detail screen SHALL show Edit/Submit/Cancel actions only when the currently authenticated
user is the expense's owner and its `Status` makes that action valid (per
`frontend-expense-submission-ui` and `frontend-expense-maintenance-ui`). A non-owner viewing an
expense they are permitted to see SHALL see the role-specific review action(s) defined by
`frontend-expense-review-actions-ui` when their role and the expense's status/category make one
applicable (Manager approve/reject, Compliance approve/reject, Finance reimburse), and otherwise
SHALL see a read-only detail view with no action controls at all. A non-owner never sees both an
owner-only action (Edit/Submit/Cancel) and a review action at the same time, since ownership and
review eligibility are mutually exclusive by role.

#### Scenario: Owner sees applicable actions
- **WHEN** the owner of a `Draft` expense views its detail
- **THEN** the frontend SHALL show the Edit, Submit, and Cancel actions, and SHALL NOT show any
  review action

#### Scenario: Non-owner with an applicable review role sees that role's action
- **WHEN** a Manager views a direct report's `Submitted` expense detail
- **THEN** the frontend SHALL show the Manager Approve/Reject actions, and SHALL NOT show any
  Edit, Submit, or Cancel action

#### Scenario: Non-owner sees a read-only view
- **WHEN** a Manager views a direct report's `Approved` expense detail (no longer `Submitted`, so
  no Manager review action applies)
- **THEN** the frontend SHALL NOT show any Edit, Submit, Cancel, or review action, and SHALL
  render a read-only view
