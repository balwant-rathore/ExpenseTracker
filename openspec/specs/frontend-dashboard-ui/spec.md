# frontend-dashboard-ui Specification

## Purpose
TBD - created by archiving change et019-dashboard-reports. Update Purpose after archive.
## Requirements
### Requirement: Dashboard Screen Renders Role-Specific Metrics
The frontend SHALL replace the current placeholder `DashboardPage` with a real dashboard that
calls `GET /api/dashboard` and renders exactly the metric fields the backend returns for the
caller's role, applying no client-side computation or additional filtering
(`docs/FRS.md` §8.1, `dashboard-summary` capability).

#### Scenario: Employee sees their three metrics
- **WHEN** an authenticated `Employee` navigates to `/dashboard`
- **THEN** the frontend SHALL call `GET /api/dashboard` and render `totalSubmitted`, `approved`,
  and `reimbursed` as returned

#### Scenario: Manager sees their four metrics
- **WHEN** an authenticated `Manager` navigates to `/dashboard`
- **THEN** the frontend SHALL render `totalSubmitted`, `approved`, `reimbursed`, and
  `pendingApprovals` as returned

#### Scenario: Finance sees their five metrics
- **WHEN** an authenticated `Finance` caller navigates to `/dashboard`
- **THEN** the frontend SHALL render `totalSubmitted`, `approved`, `reimbursed`,
  `pendingApprovals`, and `pendingReimbursements` as returned

#### Scenario: Dashboard shows a loading state while the request is in flight
- **WHEN** `GET /api/dashboard` has not yet resolved
- **THEN** the frontend SHALL render a loading state instead of empty or zeroed metric tiles

#### Scenario: Dashboard surfaces a fetch error without showing false zero counts
- **WHEN** `GET /api/dashboard` fails
- **THEN** the frontend SHALL display an error state and SHALL NOT render metric tiles showing
  zero or stale counts as if they were current

### Requirement: Compliance Officer Has No Dashboard Nav Link or Route Access
Since `GET /api/dashboard` rejects a `ComplianceOfficer` caller with `403`
(`dashboard-summary` capability's "Compliance Officer Has No Dashboard" requirement), the frontend
SHALL NOT show a "Dashboard" navigation link for that role, and SHALL redirect a `ComplianceOfficer`
who is routed to `/dashboard` (e.g. directly after login, since a shared "land on /dashboard"
convention would otherwise 403 for this role) to `/expenses` instead. This is an explicit
deviation from `docs/SDS.md` §5.1's "All users land to dashboard after successful login" note,
confirmed with the ticket owner during `/spec`.

#### Scenario: Compliance Officer's app shell has no Dashboard link
- **WHEN** an authenticated `ComplianceOfficer` views the app navigation
- **THEN** no "Dashboard" link SHALL be present

#### Scenario: Compliance Officer is redirected away from /dashboard
- **WHEN** an authenticated `ComplianceOfficer` is navigated to `/dashboard` (directly, or as the
  post-login landing route)
- **THEN** the frontend SHALL redirect to `/expenses` without calling `GET /api/dashboard` and
  without rendering the dashboard screen

#### Scenario: Other roles are unaffected by the Compliance redirect
- **WHEN** an authenticated `Employee`, `Manager`, or `Finance` caller is navigated to `/dashboard`
- **THEN** the frontend SHALL render the dashboard screen as normal, with no redirect

