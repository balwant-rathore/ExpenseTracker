## ADDED Requirements

### Requirement: Create Expense Entry Point Visibility on the List Screen
The expense list screen's "New expense" link SHALL be shown only when the authenticated user's
role is `Employee` or `Manager` — the same roles permitted to reach the `/expenses/new` route
(`frontend-route-guards` capability's Role-Based Route Restriction) — and SHALL NOT be rendered
for any other role, so a caller who cannot use the creation form is never shown a link that would
route-guard-bounce them away from it.

#### Scenario: Employee sees the New expense link
- **WHEN** an authenticated user whose role is `Employee` views the expense list screen
- **THEN** the frontend SHALL render the "New expense" link

#### Scenario: Manager sees the New expense link
- **WHEN** an authenticated user whose role is `Manager` views the expense list screen
- **THEN** the frontend SHALL render the "New expense" link

#### Scenario: Finance does not see the New expense link
- **WHEN** an authenticated user whose role is `Finance` views the expense list screen
- **THEN** the frontend SHALL NOT render the "New expense" link

#### Scenario: Compliance Officer does not see the New expense link
- **WHEN** an authenticated user whose role is `ComplianceOfficer` views the expense list screen
- **THEN** the frontend SHALL NOT render the "New expense" link
