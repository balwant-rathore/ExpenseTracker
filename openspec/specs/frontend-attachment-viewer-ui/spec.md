# frontend-attachment-viewer-ui Specification

## Purpose
TBD - created by archiving change et019-dashboard-reports. Update Purpose after archive.
## Requirements
### Requirement: View Receipt Link on Expense Detail Screen
The expense detail screen SHALL show a "View Receipt" link, labeled with the expense's
`attachmentOriginalFileName`, that opens the expense's receipt attachment using the new
`attachment-download` capability's `GET /api/attachments/{id}` endpoint. This directly addresses
the gap where the detail screen currently displays only the attachment's file name with no way to
actually open it.

#### Scenario: Detail screen shows a View Receipt link
- **WHEN** a user who is authorized to view an expense views its detail screen
- **THEN** the frontend SHALL show a "View Receipt" link labeled with the expense's
  `attachmentOriginalFileName`

### Requirement: Viewing Opens the File Inline via an Authenticated Fetch
Since `GET /api/attachments/{id}` is a protected route requiring `Authorization: Bearer
<access-token>`, the "View Receipt" link SHALL NOT be a plain anchor pointing directly at the API
route. Clicking it SHALL fetch the file through an authenticated request, then open the result in
a new browser tab as an object URL, so the browser renders the PDF/JPG/PNG inline.

#### Scenario: Clicking View Receipt fetches the file with authentication and opens a new tab
- **WHEN** a user clicks "View Receipt" on an expense's detail screen
- **THEN** the frontend SHALL call `GET /api/attachments/{id}` with the caller's
  `Authorization: Bearer` header, and on success SHALL open the returned file in a new browser tab

#### Scenario: Backend rejection is surfaced without opening a broken tab
- **WHEN** `GET /api/attachments/{id}` responds `403` or `404`
- **THEN** the frontend SHALL display the returned error and SHALL NOT open a new tab

### Requirement: View Receipt Visibility Matches Detail Screen Visibility
The "View Receipt" link SHALL be shown to exactly the same set of viewers who can already see the
expense's detail screen (`frontend-expense-visibility-ui` capability) — no additional or narrower
restriction is applied at the link level, since the backend's `attachment-download` capability
authorizes it identically to the expense itself.

#### Scenario: Any viewer who can see the expense detail also sees the receipt link
- **WHEN** a Manager, Finance, or Compliance Officer views an expense detail screen they are
  authorized to see
- **THEN** the "View Receipt" link SHALL be shown alongside the expense data

