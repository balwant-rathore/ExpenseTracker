# Functional Requirements Specification (FRS)

## Expense Management App
---

### 1. Overview

The purpose of this application is to provide an internal web-based Expense Management System that enables employees to submit expenses, managers to approve them, Compliance Officers to review applicable expenses, and Finance to reimburse employees while maintaining reporting and audit capabilities.

---

### 2. User Roles

The system shall support the following user roles:

- Employee
- Manager
- Finance
- Compliance Officer

---

### 3. Auth

#### 3.1 User Registration

**Business rule:** Users register with email, password and employeeId. No social login.

**User story:** As a new user, I want to create an account with my email and password so I can
start taking notes.

**Acceptance criteria:**

- 3.1.1 — SHALL accept `email` and `password`; email MUST be unique (case-insensitive) and a
  valid email format.
- 3.1.2 — Password MUST be at least 8 characters, containing at least one letter and one number.
- 3.1.3 — On success, the password SHALL be hashed (never stored in plaintext) and the user
  logged in immediately (access + refresh token issued).
- 3.1.4 — Registration response MUST NOT include the password hash.

**Error scenarios:**

- Duplicate email → reject, do not reveal whether it's the email or something else that's taken
  beyond "email already registered".
- Invalid email format → reject with field-level error.
- Password fails complexity rule → reject with field-level error listing which rule failed.

#### 3.2 Login

**User story:** As a returning user, I want to log in with my email and password so I can access
my notes.

**Acceptance criteria:**

- 3.2.1 — SHALL accept `email` + `password`; on match, issue a short-lived access token and a
  long-lived refresh token.
- 3.2.2 — Failed login (wrong email or wrong password) MUST return the same generic error in
  both cases — never reveal which field was wrong.
- 3.2.3 — Refresh token SHALL be persisted server-side (DB) so it can be revoked.

**Error scenarios:**

- Wrong credentials → generic "invalid email or password" error, no field-level detail.
- Missing fields → validation error.

#### 3.3 Logout

**Acceptance criteria:**

- 3.3.1 — Logout SHALL revoke (delete or invalidate) the caller's refresh token server-side.
- 3.3.2 — After logout, the old refresh token MUST NOT be usable to obtain a new access token.

#### 3.4 Forgot Password / Reset via OTP

**Business rule:** No real email is sent. The OTP is logged to the server console for manual
retrieval during development/testing.

**User story:** As a user who forgot their password, I want to request a one-time code and use
it to set a new password.

**Acceptance criteria:**

- 3.4.1 — Requesting a reset for any email SHALL return the same success response whether or not
  the email exists (no account enumeration).
- 3.4.2 — If the account exists, a numeric OTP SHALL be generated, hashed before storage, and
  logged to console with the target email.
- 3.4.3 — OTP SHALL expire after a fixed short window; an expired OTP MUST be rejected.
- 3.4.4 — OTP SHALL be single-use — once consumed for a successful reset, it cannot be reused.
- 3.4.5 — On successful reset, all of the user's existing refresh tokens SHALL be revoked
  (force re-login on all devices).
- 3.4.6 — Requesting a new OTP SHALL invalidate any previously-issued, unused OTP for that
  account — only the most recently issued OTP is valid. Added during AB-1003 spec
  clarification.

**Error scenarios:**

- Expired OTP → reject, distinct status from "wrong OTP" is not required but message must be
  clear.
- Wrong/already-used OTP → reject.
- New password fails complexity rule (see 3.1.2) → reject with field-level error.

#### 3.5 Rate Limiting

**Business rule:** Login and registration attempts are throttled to reduce brute-force and
enumeration risk. Added during AB-1002 spec clarification — not part of the original
requirements set.

**Acceptance criteria:**

- 3.5.1 — Login attempts SHALL be rate-limited per identifier (IP and/or email) within a rolling
  window; exceeding the limit SHALL reject further attempts until the window resets.
- 3.5.2 — Registration attempts SHALL be rate-limited per IP within a rolling window to prevent
  automated mass account creation.
- 3.5.3 — Forgot-password requests SHALL be rate-limited per IP within a rolling window, same as
  login/registration. Added during AB-1003 spec clarification.
- 3.5.4 — Reset-password attempts SHALL be rate-limited per IP within a rolling window, same as
  login/registration. Added during AB-1003 spec clarification.

**Error scenarios:**

- Rate limit exceeded → reject with an appropriate "too many requests" response; do not reveal
  whether the underlying credential was correct.

---

### 4. Expenses

#### 4.1 Expense Create (Submission)

**Acceptance criteria:** 
- 4.1.1 - The system shall allow employees to submit an expense with the following mandatory user fields:
	| Field | Rules Compliance |
	|------|------|
	| Expense Number | **BR-09** |
	| Expense Date | **BR-02, BR-10** |
	| Expense Category | **valid values listed in Section 6** |
	| Amount | **BR-01** |
	| Currency | **Default to Ruppes - INR** |
	| Description | **Length upto 500 characters** |
	| Receipt Attachment | **BR-03** |
	
- 4.1.2 - User can save expenses as a `Draft` for submission later. On save, Expense status is `Draft`.
- 4.1.2 - User can `Submit` expenses directly without saving a draft. On submit Expense status is `Submitted`.
- 4.1.3 - Allowed attachment types:
	- PDF
	- JPG
	- PNG
- 4.1.4 - Maximum attachment size: 10 MB
- 4.1.5 - Any Employee or Manager can submit expense.

#### 4.2 Expense Edit

**Business Rules:** Compliant with **BR-04**

**Acceptance criteria:**
- 4.2.1 - The system shall allow employees/managers to edit their own expenses while the expense is Draft or Submitted.
- 4.2.2 - The following statuses are considered editable:
	- Draft
	- Submitted

#### 4.3 Expense Cancel

**Business Rules:** Compliant with **BR-05**

**Acceptance criteria:**
- 4.3.1 - The system shall allow employees to cancel their own pending (`Submitted`) expenses.
- 4.3.2 - On cancellation, expense status changes to `Cancelled`.

#### 4.4 Expense View

**Acceptance criteria:**
- 4.4.1 - Employees can view only expenses created by them with thier current status.
- 4.4.2 - Managers can view all expenses except `Draft` expenses of the employees reporting to them.
- 4.4.3 - Finance can view all expenses except `Draft` expenses.
- 4.4.4 - Compliance officers can only view `Approved` expenses of `Client Entertainment` category.
- 4.4.5 - Complant with **BR-07** 

---

### 5. Expense Review

#### 5.1 Manager Expense Review

**Acceptance criteria:**
- 5.1.1 - The system shall allow managers to view  expenses `Submitted` by employees reporting to them.
- 5.1.2 - Approve expenses. Expenses Status change to `Approved`.
- 5.1.3 - Reject expenses. Expenses Status change to `Rejected`.
- 5.1.4 - Enter a mandaory rejection comment when rejecting an expense.
- 5.1.5 - `Rejected` expenses cannot be approved later.
- 5.1.6 - **BR-06**: Managers cannot approve their own expenses

#### 5.2 Compliance Officer Review

**Acceptance criteria:**
- 5.2.1 - Only `Client Entertainment` category expenses can be reviewed by Compliance officer.
- 5.1.2 - The system shall allow `Compliance officer` to view valid expenses `Approved` by managers
- 5.2.3 - Approve expenses. Expenses Status change to `Compliance Approved`.
- 5.2.4 - Reject expenses. Expenses Status change to `Rejected`.
- 5.2.5 - Enter a mandaory rejection comment when rejecting an expense
- 5.2.6 - `Rejected` expenses cannot be approved later.

---

### 6. Expense Categories

#### 6.1 Expense category support

**Acceptance Criteria**
- 6.1.1 - The system shall support only the following expense categories:
	- Travel
	- Hotel
	- Meals
	- Office Supplies
	- Client Entertainment
	- Training
	- Other
- 6.1.2 - No additional categories shall be supported.

---

### 7. Finance Processing

#### 7.1 Expense Search 

**Acceptance criteria**
- 7.1.1 - The system shall allow finance users to search expenses using:
	- Expense Number
	- Employee Name
	- Date Range (created date range)
	- Category
	- Status
- 7.1.2 - The system shall allow Finance users to View `Approved` expenses
- 7.1.3 - **BR-08** compliant. Mark only `Approved` or `Compliance Approved` expenses as `Reimbursed`.
- 7.1.4 - Export monthly reimbursement data with all expense details

#### 7.2 Expense Workflow

**Acceptance criteria**
- 7.2.1 - Normal workflow:
`Draft` -> `Submitted` -> `Approved` -> `Reimbursed`
- 7.2.2 - Special workflow (Applicable only for `Client Entertainment` category expenses):
`Draft` -> `Submitted` -> `Approved` -> `Compliance Approved` -> `Reimbursed`
- 7.2.3 - Rejected Workflow:
Normal - \* -> `Submitted` -> `Rejected`
Special - \* -> `Approved` -> `Rejected` (by Compliance officer)
- 7.2.4 - Expenses shall not transition back to past status.
- 7.2.5 - Employees shall submit a new expense for rejected expenses.

---

### 8. Dashboard

#### 8.1 Status Dashboard

**Acceptance criteria**
- 8.1.1 - Employee dashboard view - The system shall display counts of expenses of that employee:
	- Total Submitted
	- Approved
	- Reimbursed
- 8.1.2 - Manager Dashboard view - Display counts of expenses by employees reporting to the manager: 
	- Total Submitted 
	- Approved
	- Reimbursed
	- Pending Approvals
- 8.1.3 - Finance Dashboard view - Display counts of all expenses:
	- Total Submitted 
	- Approved
	- Reimbursed
	- Pending Approvals
	- Pending Reimbursements

---

### 9 Email Notifications

#### 9.1 Status Change Notifications
**Business Rule:** Send Email notifications when expense status changes or expense is submitted.

**Acceptance criteria**
- 9.1.1 -  When: `Submitted` Recipients: Employee and their Manager
- 9.1.2 - When: `Approved` Recipients: Employee, their Manager and Finance
- 9.1.3 - When: `Rejected` Recipients: Employee
- 9.1.4 - When: `Reimbursed` recipients: Employee, their manager and Finance
- 9.1.5 - No emails shall be sent through real servers
- 9.1.6 - All notification mails will be logged to a html file with To, CC, Subject and Body details.

---

### 10. Epense Reports

#### 10.1 Monthly report

**Acceptance criteria**
- 10.1.1 - The system shall generate a monthly reimbursement report containing:
	- Employee
	- Expense Number
	- Category
	- Amount
	- Currency
	- Approval Date
	- Reimbursement Date
- 10.1.2 - The report shall be exportable to Excel.

---


### 11. Business Rules Glossary

| Rule ID | Requirement |
|----------|-------------|
| **BR-01** | Expense Amount must be greater than zero. |
| **BR-02** | Expense Date cannot be in the future. |
| **BR-03** | Receipt attachment is mandatory. |
| **BR-04** | Only pending  expenses may be edited. |
| **BR-05** | Only pending expenses may be cancelled. |
| **BR-06** | Managers cannot approve their own expenses. |
| **BR-07** | Rejected expenses are read-only. |
| **BR-08** | Finance can reimburse only approved expenses. |
| **BR-09** | Expense number shall be generated automatically. |
| **BR-10** | All dates shall be stored in the company's local timezone. |

---

### 12. Out of Scope

Do not build these. Any attempt is a spec violation. The following capabilities are not included:

- Payment processing
- Multiple receipt attachments
- Additional expense categories
- Actual email notification
- Email server integration
- Sending Live OTP to actual emails

---
### 13. Glossary

- **OTP:** one-time password, a short-lived numeric code used for password reset.

---

### 14. Assumptions

- Every employee reports to exactly one manager.
- Every expense belongs to one employee.
- Each expense has only one receipt attachment.
- There is only one Compliance Officer role.
- Payments are processed by an external payroll/accounting system and is out of scope for this app.
- The application tracks reimbursement status only.
- Employees, Managers, finance and Compliance officer all users are already in the database in Employees table and are identified by employeeId 
- Employee table requires no change management