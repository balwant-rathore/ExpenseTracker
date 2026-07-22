## ADDED Requirements

### Requirement: Breadcrumb Trail Reflects Session Visit History
The frontend SHALL render a breadcrumb trail on every authenticated page (inside `AppLayout`)
that reflects the actual sequence of pages the user navigated through during the current session
— a dynamic stack, not a fixed route-based hierarchy — so that reaching the same route via
different navigation paths produces different trails. This directly addresses the gap where
`ExpenseDetailPage` currently offers no backward navigation at all.

#### Scenario: Reaching an expense detail from Finance Search shows the search in the trail
- **WHEN** a `Finance` caller navigates from `/finance/search` to an expense's detail route
- **THEN** the breadcrumb trail SHALL read `Home > Finance Search > <Expense Number>`

#### Scenario: Reaching the same expense detail from the expense list shows a different trail
- **WHEN** a user navigates from the expense list route to the same expense's detail route
- **THEN** the breadcrumb trail SHALL read `Home > Expenses > <Expense Number>`, not the Finance
  Search trail from the previous scenario

#### Scenario: A freshly loaded page with no prior navigation shows only Home and the current page
- **WHEN** a user opens a page directly (e.g. a hard reload or a pasted URL) with no recorded
  navigation history for the session
- **THEN** the breadcrumb trail SHALL read `Home > <current page>` only

#### Scenario: Expense detail page now offers backward navigation via the breadcrumb
- **WHEN** a user views an expense's detail screen
- **THEN** the breadcrumb trail SHALL render above the detail content, providing a clickable path
  back to the page the user came from

### Requirement: Home Crumb Resolves Per Role
The breadcrumb trail's leading "Home" crumb SHALL link to `/dashboard` for `Employee`, `Manager`,
and `Finance` callers, and to `/expenses` for a `ComplianceOfficer` caller, consistent with the
`frontend-dashboard-ui` capability's Compliance Officer redirect (Compliance has no dashboard to
link to).

#### Scenario: Home crumb links to the dashboard for non-Compliance roles
- **WHEN** an `Employee`, `Manager`, or `Finance` caller views any authenticated page
- **THEN** the "Home" crumb SHALL link to `/dashboard`

#### Scenario: Home crumb links to the expense list for Compliance Officer
- **WHEN** a `ComplianceOfficer` views any authenticated page
- **THEN** the "Home" crumb SHALL link to `/expenses`

### Requirement: Clicking a Breadcrumb Entry Navigates and Truncates the Trail
Clicking any non-current entry in the breadcrumb trail SHALL navigate to that entry's page and
discard every trail entry recorded after it, so that subsequent navigation from that point builds
a fresh trail rather than appending to the discarded one.

#### Scenario: Clicking an earlier crumb navigates to that page
- **WHEN** a user viewing `Home > Finance Search > <Expense Number>` clicks "Finance Search"
- **THEN** the frontend SHALL navigate to `/finance/search`

#### Scenario: Navigating via a breadcrumb truncates entries after it
- **WHEN** a user clicks "Finance Search" from the trail in the prior scenario, then navigates to
  a different expense's detail
- **THEN** the new trail SHALL read `Home > Finance Search > <New Expense Number>`, with no trace
  of the previously discarded expense entry
