## ADDED Requirements

### Requirement: Post-Commit Notification Triggering
The system SHALL trigger a notification for each of the following expense workflow transitions,
only after the transition's database transaction has committed successfully: `Submitted`,
`Approved`, `Rejected`, `ComplianceApproved`, `ComplianceRejected`, `Reimbursed` (FRS §9.1,
SDS §7.3). A `Submitted` notification SHALL fire regardless of whether the expense reached
`Submitted` via direct creation (`action=Submit`) or via the explicit submit action on an existing
`Draft` expense.

#### Scenario: Creating an expense directly as Submitted triggers a notification
- **WHEN** an Employee or Manager calls `POST /api/expenses` with `action: "Submit"` and the
  expense is created successfully
- **THEN** a `Submitted` notification is triggered after the create transaction commits

#### Scenario: Submitting an existing Draft expense triggers a notification
- **WHEN** an Employee or Manager calls `POST /api/expenses/{id}/submit` on their own `Draft`
  expense and the transition succeeds
- **THEN** a `Submitted` notification is triggered after the submit transaction commits

#### Scenario: Manager approval triggers a notification
- **WHEN** a Manager approves a `Submitted` expense that is not their own
- **THEN** an `Approved` notification is triggered after the approve transaction commits

#### Scenario: Manager rejection triggers a notification
- **WHEN** a Manager rejects a `Submitted` expense with a rejection comment
- **THEN** a `Rejected` notification is triggered after the reject transaction commits

#### Scenario: Compliance approval triggers a notification
- **WHEN** a Compliance Officer approves an `Approved` Client Entertainment expense
- **THEN** a `ComplianceApproved` notification is triggered after the compliance-approve
  transaction commits

#### Scenario: Compliance rejection triggers a notification
- **WHEN** a Compliance Officer rejects an `Approved` Client Entertainment expense with a
  rejection comment
- **THEN** a `ComplianceRejected` notification is triggered after the compliance-reject
  transaction commits

#### Scenario: Finance reimbursement triggers a notification
- **WHEN** Finance marks an eligible expense as `Reimbursed`
- **THEN** a `Reimbursed` notification is triggered after the reimburse transaction commits

#### Scenario: No notification is triggered when a transition fails
- **WHEN** any workflow action fails validation, authorization, or the business-rule check for its
  transition (e.g. a Manager attempts to approve their own expense)
- **THEN** no notification is triggered, since no transaction was committed

### Requirement: Notification Recipient Resolution
The system SHALL resolve notification recipients per event as follows, reading employee email
addresses from the `Employee` table (SDS §7.1):

| Event | To | CC |
|-------|----|----|
| Submitted | Employee | Reporting Manager |
| Approved | Employee | Reporting Manager, all active Finance-role employees |
| Rejected | Employee | — |
| ComplianceApproved | Employee | Reporting Manager, all active Finance-role employees |
| ComplianceRejected | Employee | — |
| Reimbursed | Employee | Reporting Manager, all active Finance-role employees |

`ComplianceApproved` reuses the `Approved` recipient pattern because it advances the same
Client-Entertainment expense to a state Finance must act on next. `ComplianceRejected` reuses the
`Rejected` recipient pattern because the resulting `Status` is `Rejected` regardless of whether a
Manager or the Compliance Officer performed the rejection (FRS §9.1.3 does not distinguish by
rejecting role).

#### Scenario: Approved notification CCs every active Finance-role employee
- **WHEN** an `Approved` notification is resolved and two active employees hold the `Finance` role
- **THEN** both Finance employees' email addresses appear in the CC list alongside the reporting
  manager

#### Scenario: Rejected notification has no CC recipients
- **WHEN** a `Rejected` notification is resolved
- **THEN** only the expense owner's email appears in the To field and the CC list is empty

#### Scenario: ComplianceApproved notification includes Finance
- **WHEN** a `ComplianceApproved` notification is resolved for a Client Entertainment expense
- **THEN** the CC list includes the reporting manager and all active Finance-role employees

#### Scenario: ComplianceRejected notification matches Rejected recipients
- **WHEN** a `ComplianceRejected` notification is resolved
- **THEN** only the expense owner's email appears in the To field and the CC list is empty,
  identical to a Manager-initiated `Rejected` notification

### Requirement: HTML Notification Log
The system SHALL write each triggered notification as an entry in an HTML log file (FRS §9.1.6,
SDS §7.4). The log file location, name, and timestamp format SHALL be read from configuration
(`NotificationLogDirectory`, `NotificationLogFileName`, `NotificationTimestampFormat`). The file
SHALL be created automatically if it does not exist, and entries SHALL be appended — never
overwritten. Each entry SHALL contain: Timestamp, Event, To, CC, Subject, and HTML Body. Entry
content SHALL NOT expose internal identifiers (e.g. `EmployeeId`, `ExpenseId` GUIDs) or any
confidential data beyond what each template defines (SDS §7.5).

#### Scenario: Log file is created on first notification
- **WHEN** a notification is triggered and the configured log file does not yet exist on disk
- **THEN** the file is created at the configured path before the entry is written

#### Scenario: Subsequent notifications append rather than overwrite
- **WHEN** a notification is triggered and the configured log file already contains prior entries
- **THEN** the new entry is appended after the existing content, which remains intact

#### Scenario: Log entry contains all required fields
- **WHEN** any notification is written to the log
- **THEN** the entry contains a Timestamp, the Event name, the To address(es), the CC address(es)
  (if any), the Subject, and the HTML Body

#### Scenario: Log entry omits internal identifiers
- **WHEN** a notification entry is rendered for any event
- **THEN** the entry contains no raw `EmployeeId` or `ExpenseId` GUID values

### Requirement: Concurrent Notification Writes Do Not Corrupt the Log
The system SHALL serialize writes to the notification log so that notifications triggered by
concurrent workflow actions never interleave into a malformed entry.

#### Scenario: Two simultaneous approvals both produce well-formed entries
- **WHEN** two different expenses are approved by two Managers at effectively the same time
- **THEN** the log file contains two distinct, non-interleaved, well-formed entries — one per
  approval

### Requirement: Notification Failures Do Not Affect the Workflow Response
The system SHALL guarantee that a failure while resolving recipients, rendering a template, or
writing the notification log never propagates as an exception out of the notification call, is
logged via the application logging framework, and never alters the workflow API's success
response (AGENTS.md, SDS §7.6).

#### Scenario: Log write failure does not fail the API request
- **WHEN** a workflow transition commits successfully but the subsequent notification log write
  fails (e.g. the configured log directory is not writable)
- **THEN** the workflow API still returns its normal success response, and the failure is recorded
  in the application logs

#### Scenario: One failed notification does not block later notifications
- **WHEN** a notification fails for one workflow transition
- **THEN** subsequent, unrelated notification triggers are unaffected and continue to be written
  normally
