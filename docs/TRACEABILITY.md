# FRS-to-Test Traceability Matrix

Maps every numbered acceptance criterion in `docs/FRS.md` §3–§10 and every business rule
`BR-01`–`BR-10` (§11) to the automated test(s) that verify it, per the `quality-release`
OpenSpec change (ET020). `Status` is `Covered` (test already existed pre-ET020) or
`Added (ET020)` (test written as part of this change). A row referencing no test yet is a
gap to close before this document is considered complete — none should remain at archive time.

**Columns:** `AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status`

**Baseline note (task 1.5):** `dotnet build backend/ExpenseTracker.sln --no-incremental` produced
**0 warnings, 0 errors** as of this audit — CI's backend build/lint step starts from a clean
baseline; any warning introduced later is genuinely new, not pre-existing noise.

---

## §3 Auth

### 3.1 User Registration

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 3.1.1 | Accept email+password; email unique (case-insensitive), valid format | Integration | `IntegrationTests/AuthRegisterTests.cs` | `DuplicateCaseInsensitiveEmailIsRejected`, `UniqueEmailIsAccepted` (uniqueness only — see gap row below for format) | Covered |
| 3.1.2 | Password ≥8 chars, ≥1 letter, ≥1 number | Unit | `UnitTests/Application/Auth/PasswordPolicyValidatorTests.cs` | `IsSatisfiedBy_ShorterThan8Characters_ReturnsFalse`, `IsSatisfiedBy_NoDigit_ReturnsFalse`, `IsSatisfiedBy_NoLetter_ReturnsFalse`, `IsSatisfiedBy_CompliantPassword_ReturnsTrue` | Covered |
| 3.1.3 | Password hashed, never plaintext; auto-login on success (tokens issued) | Integration + Unit | `IntegrationTests/AuthRegisterTests.cs`; `UnitTests/.../BCryptPasswordHasherTests.cs` | `SuccessfulRegistrationReturnsTokensAndAUserWithoutThePasswordHash`, `PasswordIsPersistedOnlyAsAHash`; `HashAndVerify_SamePassword_Succeeds` | Covered |
| 3.1.4 | Registration response MUST NOT include password hash | Integration | `IntegrationTests/AuthRegisterTests.cs` | `SuccessfulRegistrationReturnsTokensAndAUserWithoutThePasswordHash` | Covered |
| — | Error: duplicate email → generic rejection (no field disclosure) | Integration | `IntegrationTests/AuthRegisterTests.cs` | `DuplicateCaseInsensitiveEmailIsRejected` (409, empty `fields`) | Covered |
| — | Error: invalid email format → field-level error | Integration | `IntegrationTests/AuthRegisterTests.cs` | `MalformedEmail_ReturnsValidationErrorOnEmailField` (new — see gap note below) | Added (ET020) |
| — | Error: password complexity failure → field-level error | Integration | `IntegrationTests/AuthRegisterTests.cs` | `NonCompliantPasswordIsRejectedWithAFieldLevelError` | Covered |

> **Gap found and closed (task 2.9):** `RegisterRequestValidator`'s `.EmailAddress()` rule
> existed in source but no test ever submitted a syntactically-malformed email (e.g.
> `"not-an-email"`) to `/api/auth/register` and asserted the field-level error response.
> Added `MalformedEmail_ReturnsValidationErrorOnEmailField` to
> `IntegrationTests/AuthRegisterTests.cs`.

### 3.2 Login

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 3.2.1 | Accept email+password; issue access + refresh token on match | Integration | `IntegrationTests/AuthLoginTests.cs` | `CorrectCredentialsIssueATokenPair` | Covered |
| 3.2.2 | Wrong email or wrong password → identical generic error | Integration | `IntegrationTests/AuthLoginTests.cs` | `WrongPasswordReturnsTheIdenticalGenericError` | Covered |
| 3.2.3 | Refresh token persisted server-side (revocable) | Unit + Integration | `UnitTests/.../RefreshTokenServiceTests.cs`, `RefreshTokenConfigurationTests.cs`; `IntegrationTests/AuthLoginTests.cs` | `IssueAsync_PersistsOnlyTokenHash_NeverRawValue`, `RefreshToken_OnlyStoresTokenHash_NoPlaintextTokenProperty`; `SuccessfulLoginPersistsARedeemableRefreshToken` | Covered |
| — | Error: missing fields → validation error | Integration | `IntegrationTests/AuthLoginTests.cs` | `MissingRequiredFieldsReturnAValidationError` | Covered |

### 3.3 Logout

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 3.3.1 | Logout revokes caller's refresh token server-side | Integration | `IntegrationTests/AuthLogoutTests.cs` | `LogoutRevokesTheSuppliedTokenAndLeavesOtherSessionsActive`, `SuccessfulLogoutReturns204` | Covered |
| 3.3.2 | Revoked refresh token can't be used to get a new access token | Integration | `IntegrationTests/AuthLogoutTests.cs` | `RevokedTokenCannotBeUsedToObtainANewAccessToken` | Covered |

### 3.4 Forgot Password / Reset via OTP

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 3.4.1 | Forgot-password returns identical response whether/not email exists | Integration | `IntegrationTests/AuthForgotPasswordTests.cs` | `NonExistingAccount_ReturnsIdenticalGenericSuccess` | Covered |
| 3.4.2 | OTP generated, hashed before storage, logged to console | Unit | `UnitTests/Application/Auth/PasswordResetOtpServiceTests.cs` | `RequestResetAsync_PersistsOtpAsHash_NeverPlaintext`, `RequestResetAsync_WritesPlaintextOtpAndEmailToConsole` | Covered |
| 3.4.3 | OTP expires after fixed window; expired OTP rejected | Unit + Integration | `PasswordResetOtpServiceTests.cs`; `IntegrationTests/AuthResetPasswordTests.cs` | `RequestResetAsync_ExpiresAfterConfiguredMinutes`, `VerifyAndConsumeAsync_ExpiredOtp_ReturnsExpired`; `ExpiredOtp_ReturnsResourceExpired` | Covered |
| 3.4.4 | OTP single-use; can't be reused after successful reset | Unit + Integration | `PasswordResetOtpServiceTests.cs`; `AuthResetPasswordTests.cs` | `VerifyAndConsumeAsync_ValidOtp_SucceedsAndMarksUsed`, `VerifyAndConsumeAsync_AlreadyUsedOtp_ReturnsInvalid`; `AlreadyUsedOtp_IsRejectedAndCannotBeReusedASecondTime` | Covered |
| 3.4.5 | Successful reset revokes all of the user's existing refresh tokens | Integration + Unit | `AuthResetPasswordTests.cs`; `UnitTests/.../RefreshTokenServiceTests.cs` | `SuccessfulReset_RevokesAllActiveRefreshTokensForUser`; `RevokeAllAsync_RevokesActiveTokens_LeavesRevokedAndExpiredUnchanged` | Covered |
| 3.4.6 | Requesting a new OTP invalidates any prior unused OTP | Unit | `PasswordResetOtpServiceTests.cs` | `RequestResetAsync_SecondRequest_InvalidatesFirstOtp` | Covered |
| — | Error: expired OTP → reject | Integration | `AuthResetPasswordTests.cs` | `ExpiredOtp_ReturnsResourceExpired` | Covered |
| — | Error: wrong/already-used OTP → reject | Integration | `AuthResetPasswordTests.cs` | `WrongOtp_ReturnsAuthenticationFailed`, `AlreadyUsedOtp_IsRejectedAndCannotBeReusedASecondTime` | Covered |
| — | Error: new password fails complexity → field-level error | Integration | `AuthResetPasswordTests.cs` | `WeakNewPassword_ReturnsValidationErrorOnNewPasswordField` | Covered |

### 3.5 Rate Limiting

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 3.5.1 | Login rate-limited per identifier within rolling window | Integration | `IntegrationTests/RateLimitingTests.cs` | `RequestsWithinConfiguredLimit_ArePermitted`, `RequestExceedingConfiguredLimit_Returns429`, `RateLimitedResponse_IsIdenticalRegardlessOfRequestContent` (via synthetic route wired to the real `Login` named policy — real `/api/auth/login` itself isn't driven to 429 since the test factory raises its limit to 1000 to avoid flakiness) | Covered |
| 3.5.2 | Registration rate-limited per IP within rolling window | Integration | `IntegrationTests/RateLimitingTests.cs` | `RegisterRequestsExceedingConfiguredLimit_Return429` (new — see gap note below) | Added (ET020) |
| 3.5.3 | Forgot-password rate-limited per IP within rolling window | Integration | `IntegrationTests/RateLimitingTests.cs` | `ForgotPasswordRequestsExceedingConfiguredLimit_Return429` | Covered |
| 3.5.4 | Reset-password rate-limited per IP within rolling window | Integration | `IntegrationTests/RateLimitingTests.cs` | `ResetPasswordRequestsExceedingConfiguredLimit_Return429` | Covered |

> **Gap found and closed (task 2.9):** unlike Login/ForgotPassword/ResetPassword, no synthetic
> test route existed for the `AuthRateLimitPolicyNames.Register` policy in
> `TestEndpointsStartupFilter.cs`, and `AuthRegisterTests.cs` deliberately caps every test at
> ≤2 calls to avoid tripping the real limit — so `[EnableRateLimiting(...Register)]` on
> `AuthController.Register` was wired but never functionally exercised. Added a synthetic
> `/__test/rate-limited-register` route (mapped to the same `Register` named policy) and
> `RegisterRequestsExceedingConfiguredLimit_Return429` to `RateLimitingTests.cs`, mirroring the
> existing Login/ForgotPassword/ResetPassword pattern.

---

## §4 Expenses

### 4.1 Expense Create (Submission)

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 4.1.1 — Expense Number | Mandatory field, compliant with BR-09 | Unit + Integration | `UnitTests/Application/Expenses/ExpenseNumberGeneratorTests.cs`; `IntegrationTests/ExpenseSubmissionTests.cs` | `GenerateAsync_ReflectsCompanyClockDate`, `GenerateAsync_IncrementsPastExistingSameDayCount`; `Create_ClientSuppliedExpenseNumber_IsIgnored` | Covered |
| 4.1.1 — Expense Date | Mandatory field, compliant with BR-02, BR-10 | Unit | `CreateExpenseRequestValidatorTests.cs`; `UnitTests/.../ExpenseServiceTests.cs` | `MissingExpenseDate_Fails`; `CreateAsync_FutureExpenseDate_Fails`, `UpdateAsync_FutureExpenseDate_Fails` (BR-02 only — BR-10 storage aspect flagged separately below) | Covered (BR-02); see BR-10 flag |
| 4.1.1 — Expense Category | Mandatory field, valid values only (§6) | Unit | `CreateExpenseRequestValidatorTests.cs`; `UnitTests/Domain/Enums/ExpenseCategoryTests.cs` | `CategoryOutsideDefinedValues_Fails`, `MissingCategory_FailsWithoutThrowing`; `ExpenseCategory_ContainsExactlySevenValues` | Covered |
| 4.1.1 — Amount | Mandatory field, compliant with BR-01 | Unit | `UnitTests/.../ExpenseServiceTests.cs` | `CreateAsync_AmountNotPositive_Fails` (Theory), `UpdateAsync_AmountNotPositive_Fails` | Covered |
| 4.1.1 — Currency | Mandatory field, defaults to INR | Unit | `CreateExpenseRequestValidatorTests.cs` | `CurrencyOtherThanInr_Fails`, `MissingCurrency_FailsWithoutThrowing` | Covered — see ambiguity flag below |
| 4.1.1 — Description | Mandatory field, ≤500 characters | Unit | `CreateExpenseRequestValidatorTests.cs` | `MissingDescription_Fails`, `DescriptionOverFiveHundredCharacters_Fails`, `DescriptionAtFiveHundredCharacters_Passes` | Covered |
| 4.1.1 — Receipt Attachment | Mandatory field, compliant with BR-03 | Unit | `UnitTests/.../ExpenseServiceTests.cs`; `ExpenseConfigurationTests.cs` | `CreateAsync_AttachmentNotFound_Fails`; `Expense_AttachmentId_IsUniqueIndex` | Covered |
| 4.1.2 (Draft) | Save as Draft → status `Draft` | Integration + Unit | `IntegrationTests/ExpenseSubmissionTests.cs`; `UnitTests/.../ExpenseServiceTests.cs` | `Create_DraftAction_Returns201WithDraftStatus`; `CreateAsync_Draft_HasNoSubmittedAt`, `CreateAsync_Draft_SendsNoNotification` | Covered |
| 4.1.2 (Submit) | Submit directly → status `Submitted` | Integration + Unit | `ExpenseSubmissionTests.cs`; `ExpenseServiceTests.cs` | `Create_SubmitAction_Returns201WithSubmittedStatus`; `CreateAsync_Submit_HasSubmittedAtPopulated`, `CreateAsync_Submit_SendsSubmittedNotificationAfterCommit` | Covered |
| 4.1.3 | Allowed attachment types: PDF, JPG, PNG | Unit + Integration | `UnitTests/Application/Attachments/UploadAttachmentRequestValidatorTests.cs`; `IntegrationTests/AttachmentUploadTests.cs` | `ValidRequest_Passes` (Theory), `DisallowedExtension_Fails`, `DisallowedContentType_WithAllowedExtension_Fails`; `DisallowedFileType_Returns400ValidationError_NoRowCreated` | Covered |
| 4.1.4 | Max attachment size: 10 MB | Unit + Integration | `UploadAttachmentRequestValidatorTests.cs`; `AttachmentUploadTests.cs` | `OversizedFile_Fails`, `FileExactlyAtTenMegabyteBoundary_Passes`; `OversizedFile_Returns400ValidationError_NoRowCreated` | Covered |
| 4.1.5 | Any Employee or Manager can submit an expense | Integration | `IntegrationTests/ExpenseSubmissionTests.cs`; `ExpenseVisibilityTests.cs` | `Create_RoleOutsideEmployeeOrManager_Returns403` (Theory: Finance, ComplianceOfficer); `GetAll_Manager_OwnDraftExpense_IsIncluded` | Covered |

> **Audit suspicion checked and ruled out (not a gap):** the audit agent flagged Receipt
> Attachment's "missing field" path as only exercised via a nonexistent-GUID, never a
> genuinely omitted `receiptAttachmentId` JSON key — raising the ET007 "required-field bypasses
> validator" trap (`backend/CLAUDE.md` Gotchas). Verified against the actual DTO
> (`CreateExpenseRequest.cs`): `ReceiptAttachmentId` is a plain non-nullable `Guid` (not
> `required Guid`, not an enum), so an omitted JSON key binds to `Guid.Empty` via
> `System.Text.Json`'s normal struct-default behavior — it does NOT bypass the validator/service
> the way `required`-or-enum properties do. `CreateExpenseRequestValidator` has no rule at all
> for this field, so `Guid.Empty` reaches `ExpenseService.CreateAsync` and hits
> `_attachmentRepository.GetByIdAsync(Guid.Empty, ...)` — the exact same "not found" path
> `CreateAsync_AttachmentNotFound_Fails` already exercises with a different nonexistent GUID. A
> dedicated omitted-field test would be a byte-for-byte duplicate assertion of an existing test,
> not new coverage — not added.
>
> **Ambiguity flagged, not silently resolved (Currency "defaults to INR"):** `docs/FRS.md` 4.1.1
> lists Currency as a mandatory field with the rule "Default to Rupees - INR," but the
> implemented/tested behavior is stricter: `CurrencyOtherThanInr_Fails` and
> `MissingCurrency_FailsWithoutThrowing` both show currency must be explicitly supplied as
> `"INR"` — an omitted currency is REJECTED, not defaulted. This is a plausible reading (single
> supported currency, always explicit) but is not what "defaults to" literally says. Per
> `AGENTS.md` §13 / proposal.md's scope boundary, this is flagged here rather than silently
> changed — changing production code to actually default an omitted currency would be a
> business-logic change outside ET020's "verify and gate, don't alter behavior" scope.
> **Tracked as ET021** (`docs/TICKETS.md`) for future resolution.

### 4.2 Expense Edit

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 4.2.1 | Owner can edit own expense while Draft or Submitted | Integration | `IntegrationTests/ExpenseMaintenanceTests.cs` | `Update_OwnerOnDraft_Returns200WithUpdatedFields`, `Update_OwnerOnSubmitted_Returns200_StatusRemainsSubmitted`, `Update_NonOwner_Returns403` | Covered |
| 4.2.2 | Editable statuses: Draft, Submitted only | Integration + Unit | `ExpenseMaintenanceTests.cs`; `UnitTests/.../ExpenseServiceTests.cs` | `Update_ApprovedExpense_Returns422`; `UpdateAsync_NonEditableStatus_ReturnsNotEditable` (Theory: Approved, ComplianceApproved, Rejected, Cancelled, Reimbursed) | Covered |

### 4.3 Expense Cancel

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 4.3.1 | Owner can cancel own `Submitted` expense | Integration | `IntegrationTests/ExpenseMaintenanceTests.cs` | `Cancel_OwnerOnDraft_Returns200StatusCancelled`, `Cancel_OwnerOnSubmitted_Returns200StatusCancelled`, `Cancel_NonOwner_Returns403` | Covered |
| 4.3.2 | Cancellation → status `Cancelled` | Integration + Unit | `ExpenseMaintenanceTests.cs`; `UnitTests/.../ExpenseServiceTests.cs` | `Cancel_AlreadyCancelledExpense_Returns422StatusUnchanged`; `CancelAsync_NonCancellableStatus_ReturnsNotCancellable` (Theory) | Covered |

### 4.4 Expense View

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 4.4.1 | Employees view only their own expenses, with current status | Integration | `IntegrationTests/ExpenseVisibilityTests.cs` | `GetAll_Employee_SeesOnlyOwnExpenses`, `GetById_NonOwner_Returns403` | Covered |
| 4.4.2 | Managers view all non-Draft expenses of direct reports | Integration + Unit | `ExpenseVisibilityTests.cs` (both layers) | `GetAll_Manager_DirectReportsNonDraftExpense_IsIncluded`, `GetAll_Manager_IndirectReportsExpense_IsExcluded`, `GetAll_Manager_UnrelatedEmployeesExpense_IsExcluded` | Covered |
| 4.4.3 | Finance views all non-Draft expenses | Integration + Unit | `ExpenseVisibilityTests.cs` (both layers) | `GetAll_Finance_SeesNonDraft_ExcludesDraft`; `GetByIdAsync_FinanceAnyNonDraftExpense_ReturnsExpense`, `GetByIdAsync_FinanceDraftExpense_ReturnsNotVisible` | Covered |
| 4.4.4 | Compliance views only `Approved` `ClientEntertainment` expenses | Integration | `IntegrationTests/ExpenseVisibilityTests.cs` | `GetAll_Compliance_SeesApprovedAndComplianceApprovedClientEntertainment`, `GetAll_Compliance_ExcludesNonClientEntertainmentApproved`, `GetAll_Compliance_ExcludesDraftOrSubmittedClientEntertainment` | Covered — see wording note below |
| 4.4.5 | Compliant with BR-07 (rejected expenses read-only) | Unit | `UnitTests/.../ExpenseServiceTests.cs` | `UpdateAsync_NonEditableStatus_ReturnsNotEditable(Rejected)`, `CancelAsync_NonCancellableStatus_ReturnsNotCancellable(Rejected)`, `ApproveAsync_NonSubmittedStatus_ReturnsNotSubmitted(Rejected)`, `RejectAsync_NonSubmittedStatus_ReturnsNotSubmitted(Rejected)` | Covered |

> **Wording note (not a defect):** FRS 4.4.4 literally says Compliance sees only `Approved`
> ClientEntertainment expenses, but the implementation (and its tests) also shows
> `ComplianceApproved` items — i.e. expenses the Compliance Officer already actioned remain
> visible afterward, which is standard "don't lose visibility into what you just approved"
> behavior and is internally consistent with the rest of the workflow. Not flagged as a defect;
> noted so the letter-vs-intent gap is documented rather than silently glossed over.

---

## §5 Expense Review

### 5.1 Manager Expense Review

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 5.1.1 | Manager views expenses Submitted by direct reports | Integration + Unit | `IntegrationTests/ExpenseVisibilityTests.cs`; `UnitTests/Application/Expenses/ExpenseVisibilityTests.cs` | `GetAll_Manager_DirectReportsNonDraftExpense_IsIncluded`, `GetAll_Manager_IndirectReportsExpense_IsExcluded`; `Manager_DirectReportsNonDraftExpense_IsVisible` | Covered |
| 5.1.2 | Approve → status `Approved` | Integration + Unit | `IntegrationTests/ExpenseApprovalTests.cs`; `UnitTests/.../ExpenseServiceTests.cs` | `Approve_DirectManagerOfReport_Returns200Approved`; `ApproveAsync_DirectManagerOfReport_TransitionsToApproved` | Covered |
| 5.1.3 | Reject → status `Rejected` | Integration + Unit | `IntegrationTests/ExpenseApprovalTests.cs`; `UnitTests/.../ExpenseServiceTests.cs` | `Reject_DirectManagerOfReportWithComment_Returns200Rejected`; `RejectAsync_DirectManagerOfReport_TransitionsToRejected_SetsRejectionComment` | Covered |
| 5.1.4 | Mandatory rejection comment on reject | Integration | `IntegrationTests/ExpenseApprovalTests.cs` | `Reject_MissingComment_Returns400WithFieldsEntry`, `Reject_WhitespaceOnlyComment_Returns400`, `Reject_CommentOverFiveHundredCharacters_Returns400` | Covered |
| 5.1.5 | Rejected expenses cannot later be approved | Integration + Unit | `IntegrationTests/ExpenseApprovalTests.cs`; `UnitTests/.../ExpenseServiceTests.cs` | `Approve_RejectedExpense_Returns422StatusRemainsRejected`; `ApproveAsync_NonSubmittedStatus_ReturnsNotSubmitted` (Theory incl. Rejected) | Covered |
| 5.1.6 | BR-06: Managers cannot approve their own expenses | Integration + Unit | `IntegrationTests/ExpenseApprovalTests.cs`; `UnitTests/.../ExpenseServiceTests.cs` | `Approve_OwnExpense_Returns403`, `Reject_OwnExpense_Returns403`; `ApproveAsync_OwnExpense_ReturnsNotAuthorizedReviewer`, `RejectAsync_OwnExpense_ReturnsNotAuthorizedReviewer` | Covered |

### 5.2 Compliance Officer Review

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 5.2.1 | Only `ClientEntertainment` expenses reviewed by Compliance | Integration + Unit | `IntegrationTests/ExpenseComplianceReviewTests.cs`; `UnitTests/.../ExpenseServiceTests.cs` | `ComplianceApprove_TravelCategoryApprovedExpense_Returns422NotClientEntertainment`, `ComplianceReject_MealsCategoryApprovedExpense_Returns422NotClientEntertainment`; `ComplianceApproveAsync_NonClientEntertainmentCategory_ReturnsNotClientEntertainment` | Covered |
| 5.1.2 (sic — documented as 5.2.2) | Compliance views manager-`Approved` valid expenses | Integration + Unit | `IntegrationTests/ExpenseVisibilityTests.cs`; `UnitTests/Application/Expenses/ExpenseVisibilityTests.cs` | `GetAll_Compliance_SeesApprovedAndComplianceApprovedClientEntertainment`, `GetAll_Compliance_ExcludesNonClientEntertainmentApproved`, `GetAll_Compliance_ExcludesDraftOrSubmittedClientEntertainment` | Covered |
| 5.2.3 | Approve → status `ComplianceApproved` | Integration + Unit | `IntegrationTests/ExpenseComplianceReviewTests.cs`; `UnitTests/.../ExpenseServiceTests.cs` | `ComplianceApprove_ApprovedClientEntertainment_Returns200ComplianceApproved`, `ComplianceApprove_Success_SetsComplianceApprovedAtAndComplianceApprovedByEmployeeId`; `ComplianceApproveAsync_ApprovedClientEntertainment_TransitionsToComplianceApproved` | Covered |
| 5.2.4 | Reject → status `Rejected` | Integration + Unit | `IntegrationTests/ExpenseComplianceReviewTests.cs`; `UnitTests/.../ExpenseServiceTests.cs` | `ComplianceReject_ApprovedClientEntertainmentWithComment_Returns200Rejected`; `ComplianceRejectAsync_ApprovedClientEntertainment_TransitionsToRejected_SetsRejectionComment` | Covered |
| 5.2.5 | Mandatory rejection comment on reject | Integration | `IntegrationTests/ExpenseComplianceReviewTests.cs` | `ComplianceReject_MissingComment_Returns400WithFieldsEntry`, `ComplianceReject_WhitespaceOnlyComment_Returns400`, `ComplianceReject_CommentOverFiveHundredCharacters_Returns400` | Covered |
| 5.2.6 | Rejected expenses cannot later be approved | Integration + Unit | `IntegrationTests/ExpenseComplianceReviewTests.cs`; `UnitTests/.../ExpenseServiceTests.cs` | `ComplianceApprove_RejectedExpense_Returns422StatusRemainsRejected`; `ComplianceApproveAsync_NonApprovedStatus_ReturnsNotApprovedForCompliance` (Theory incl. Rejected) | Covered |

> **FRS documentation defect (flagged, not corrected):** `docs/FRS.md` §5 has two scenarios both
> numbered `5.1.2` — one under §5.1 (Manager: "Approve expenses...") and one under §5.2
> (Compliance: "...shall allow Compliance officer to view valid expenses..."). The second is a
> typo that should read `5.2.2`. This matrix lists it under §5.2 as `5.1.2 (sic — documented as
> 5.2.2)` per `design.md` Decision 1, rather than silently editing `docs/FRS.md` — correcting the
> FRS text is outside ET020's scope.

---

## §6 Expense Categories

### 6.1 Expense Category Support

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 6.1.1 | Exactly 7 supported categories (Travel, Hotel, Meals, OfficeSupplies, ClientEntertainment, Training, Other) | Unit | `UnitTests/Domain/Enums/ExpenseCategoryTests.cs` | `ExpenseCategory_ContainsExactlySevenValues` | Covered |
| 6.1.2 | No additional categories supported | Unit + Integration | `UnitTests/Application/Expenses/CreateExpenseRequestValidatorTests.cs`; `IntegrationTests/ExpenseMaintenanceTests.cs` | `CategoryOutsideDefinedValues_Fails`; `Update_InvalidCategory_Returns400WithFieldsEntry` | Covered |

---

## §7 Finance Processing

### 7.1 Expense Search

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 7.1.1 | Search by Expense Number, Employee Name, Date Range, Category, Status | Integration | `IntegrationTests/ExpenseSearchTests.cs` | `Search_ExpenseNumberFilter_MatchesExactly`, `Search_EmployeeNameFilter_MatchesSubstringCaseInsensitively`, `Search_CategoryAndDateRangeFilters_CombineWithAnd`, `Search_ExplicitStatusDraft_ReturnsEmptyResult` | Covered |
| 7.1.2 | Finance can view `Approved` expenses | Integration | `IntegrationTests/ExpenseVisibilityTests.cs` | `GetAll_Finance_ApprovedExpense_IsIncluded` (new — see gap note below) | Added (ET020) |
| 7.1.3 | BR-08: only `Approved`/`ComplianceApproved` expenses can be marked `Reimbursed` | Integration | `IntegrationTests/ExpenseReimbursementTests.cs` | `Reimburse_ApprovedNonClientEntertainment_Returns200Reimbursed`, `Reimburse_ComplianceApprovedClientEntertainment_Returns200Reimbursed`, `Reimburse_NonEligibleStatus_Returns422StatusUnchanged` (Theory) | Covered |
| 7.1.4 | Export monthly reimbursement data with all expense details | Integration | `IntegrationTests/MonthlyReimbursementReportTests.cs` | `MonthlyReimbursement_HeaderRow_MatchesFrsFieldOrder`, `MonthlyReimbursement_Finance_Returns200WithMatchingRecords` (same implementation/tests satisfy §10.1 — no separate export feature) | Covered |

> **Gap found and closed (task 2.9):** `ExpenseVisibilityTests.cs` only used a `Submitted`-status
> expense to prove Finance visibility (the underlying rule is a generic `Status != Draft` check,
> so this was inferred rather than directly asserted for `Approved`). Added
> `GetAll_Finance_ApprovedExpense_IsIncluded` to `IntegrationTests/ExpenseVisibilityTests.cs`.

### 7.2 Expense Workflow

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 7.2.1 | Standard workflow: Draft→Submitted→Approved→Reimbursed | Integration | `ExpenseSubmissionTests.cs`, `ExpenseApprovalTests.cs`, `ExpenseReimbursementTests.cs` | `Create_SubmitAction_Returns201WithSubmittedStatus`, `Approve_DirectManagerOfReport_Returns200Approved`, `Reimburse_ApprovedNonClientEntertainment_Returns200Reimbursed` (composed step tests) | Covered |
| 7.2.2 | ClientEntertainment workflow: adds ComplianceApproved step | Integration | `ExpenseComplianceReviewTests.cs`, `ExpenseReimbursementTests.cs` | `ComplianceApprove_ApprovedClientEntertainment_Returns200ComplianceApproved`; `Reimburse_ComplianceApprovedClientEntertainment_Returns200Reimbursed`, `Reimburse_ApprovedClientEntertainmentNotYetComplianceApproved_Returns422StatusUnchanged` (proves the step can't be skipped) | Covered |
| 7.2.3 | Rejected workflow (by Manager or Compliance) | Integration | `ExpenseApprovalTests.cs`, `ExpenseComplianceReviewTests.cs` | `Reject_DirectManagerOfReportWithComment_Returns200Rejected`; `ComplianceReject_ApprovedClientEntertainmentWithComment_Returns200Rejected` | Covered |
| 7.2.4 | No backward status transitions | Integration | `IntegrationTests/ExpenseApprovalTests.cs` | `Approve_ComplianceApprovedExpense_Returns422StatusUnchanged` (new — see gap note below) | Added (ET020) |
| 7.2.5 | Employees must submit a new expense after rejection (no resubmit) | Unit | `UnitTests/.../ExpenseServiceTests.cs` | `UpdateAsync_NonEditableStatus_ReturnsNotEditable`, `CancelAsync_NonCancellableStatus_ReturnsNotCancellable` (Theory incl. Rejected) — no resubmit endpoint exists, so Rejected is fully terminal | Covered |

> **Gap found and closed (task 2.9):** every existing "guard" test (`Submit_AlreadySubmittedExpense_Returns422`,
> `Approve_AlreadyApprovedExpense_Returns422`, etc.) only proves same-status re-processing and
> terminal-state (Rejected) rejection are blocked — none actually attempts a genuine backward
> transition (e.g. re-approving an already-`ComplianceApproved` expense). Added
> `Approve_ComplianceApprovedExpense_Returns422StatusUnchanged` to
> `IntegrationTests/ExpenseApprovalTests.cs` to close that gap.

---

## §8 Dashboard

### 8.1 Status Dashboard

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 8.1.1 | Employee dashboard: Total Submitted, Approved, Reimbursed (own) | Unit | `UnitTests/Application/Dashboard/DashboardServiceTests.cs` | `GetEmployeeDashboardAsync_*` (own-expense counting tests) | Covered |
| 8.1.2 | Manager dashboard: + Pending Approvals (direct reports, excl. own) | Unit | `UnitTests/Application/Dashboard/DashboardServiceTests.cs`, `DashboardScopeTests.cs` | `GetManagerDashboardAsync_OwnExpensesExcludedFromEveryMetric`, `GetManagerDashboardAsync_CountsDirectReportsSubmittedExpense`, `GetManagerDashboardAsync_IndirectReportsExpenseIsNotCounted`, `Manager_ExcludesOwnExpense` | Covered |
| 8.1.3 | Finance dashboard: + Pending Reimbursements (org-wide) | Unit + Integration | `UnitTests/Application/Dashboard/DashboardServiceTests.cs`; `IntegrationTests/DashboardTests.cs` | `GetFinanceDashboardAsync_CountsAcrossAllEmployees`, `GetFinanceDashboardAsync_PendingReimbursementsExcludesApprovedClientEntertainment`, `..._IncludesComplianceApprovedClientEntertainment`; `Get_Manager_ResponseOmitsPendingReimbursements` | Covered |

---

## §9 Email Notifications

### 9.1 Status Change Notifications

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 9.1.1 | Submitted → notify Employee + Manager | Unit + Integration | `UnitTests/Infrastructure/Notifications/HtmlNotificationServiceTests.cs`; `IntegrationTests/ExpenseNotificationTests.cs` | `NotifyAsync_Submitted_ResolvesOwnerAndManager`, `NotifyAsync_Submitted_OwnerHasNoManager_OmitsManagerCc`; `CreateWithSubmitAction_AppendsSubmittedEntry` | Covered |
| 9.1.2 | Approved → notify Employee, Manager, Finance | Unit + Integration | `HtmlNotificationServiceTests.cs`; `ExpenseNotificationTests.cs` | `NotifyAsync_Approved_CcsManagerAndEveryActiveFinanceEmployee`; `Approve_AppendsApprovedEntry` | Covered |
| 9.1.3 | Rejected → notify Employee | Unit + Integration | `HtmlNotificationServiceTests.cs`; `ExpenseNotificationTests.cs` | `NotifyAsync_Rejected_HasEmptyCcList`; `Reject_AppendsRejectedEntry` | Covered |
| 9.1.4 | Reimbursed → notify Employee, Manager, Finance | Unit | `HtmlNotificationServiceTests.cs` | `NotifyAsync_Reimbursed_CcsManagerAndFinance` (new — see gap note below) | Added (ET020) |
| 9.1.5 | No emails sent through real servers | Integration (structural) | `IntegrationTests/NotificationServiceCollectionExtensionsDiTests.cs` | `AddNotificationFoundation_RegistersAllNotificationServices_ResolvableFromDi` (asserts `INotificationService` resolves to `HtmlNotificationService`; repo-wide search confirms no SMTP/MailKit/SendGrid dependency exists) | Covered |
| 9.1.6 | Notifications logged to HTML file (To/CC/Subject/Body) | Unit + Integration | `UnitTests/Infrastructure/Notifications/NotificationLogWriterTests.cs`; `ExpenseNotificationTests.cs` | `AppendAsync_LogFileDoesNotExist_CreatesItBeforeWriting`, `AppendAsync_TwoConcurrentCalls_BothEntriesAreWellFormedAndNotInterleaved`; `AssertWellFormedEntry` helper (used by every scenario test) | Covered |

> **Gap found and closed (task 2.9):** 9.1.4 had only a structural integration assertion
> (`Reimburse_AppendsReimbursedEntry` checks a well-formed log entry exists, not who's on
> it) — no test asserted Manager/Finance actually appear in a Reimbursed notification's
> To/CC, unlike the equivalent Approved test. Added
> `NotifyAsync_Reimbursed_CcsManagerAndFinance` to
> `UnitTests/Infrastructure/Notifications/HtmlNotificationServiceTests.cs`, mirroring
> `NotifyAsync_Approved_CcsManagerAndEveryActiveFinanceEmployee`.

---

## §10 Expense Reports

### 10.1 Monthly Report

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 10.1.1 | Monthly reimbursement report: Employee, Expense#, Category, Amount, Currency, Approval Date, Reimbursement Date | Unit + Integration | `UnitTests/Infrastructure/Reporting/ClosedXmlMonthlyReimbursementReportGeneratorTests.cs`; `IntegrationTests/MonthlyReimbursementReportTests.cs` | `Generate_HeaderRow_MatchesFrsFieldOrder`, `Generate_SingleRecord_WritesRowValuesAndDateFormatting`; `MonthlyReimbursement_HeaderRow_MatchesFrsFieldOrder`, `MonthlyReimbursement_DateCells_UseExcelDateFormatting` | Covered |
| 10.1.2 | Exportable to Excel (.xlsx) | Integration | `IntegrationTests/MonthlyReimbursementReportTests.cs` | `MonthlyReimbursement_Finance_Returns200WithMatchingRecords`, `MonthlyReimbursement_SingleDigitMonth_ZeroPadsFileName` | Covered |

---

## §11 Business Rules Glossary (BR-01–BR-10)

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| BR-01 | Expense Amount must be greater than zero | Unit | `UnitTests/.../ExpenseServiceTests.cs` | `CreateAsync_AmountNotPositive_Fails` (Theory), `UpdateAsync_AmountNotPositive_Fails` | Covered |
| BR-02 | Expense Date cannot be in the future | Unit | `UnitTests/.../ExpenseServiceTests.cs` | `CreateAsync_FutureExpenseDate_Fails`, `UpdateAsync_FutureExpenseDate_Fails`, `CreateAsync_UsesCompanyClock_NotRawUtcNow_ForFutureDateCheck` | Covered |
| BR-03 | Receipt attachment is mandatory | Unit | `UnitTests/.../ExpenseServiceTests.cs` | `CreateAsync_AttachmentNotFound_Fails` (see 4.1.1 note — omitted-key case reduces to this same path) | Covered |
| BR-04 | Only pending (Draft/Submitted) expenses may be edited | Integration + Unit | `ExpenseMaintenanceTests.cs`; `ExpenseServiceTests.cs` | `Update_ApprovedExpense_Returns422`; `UpdateAsync_NonEditableStatus_ReturnsNotEditable` (Theory) | Covered |
| BR-05 | Only pending (Submitted) expenses may be cancelled | Integration + Unit | `ExpenseMaintenanceTests.cs`; `ExpenseServiceTests.cs` | `Cancel_AlreadyCancelledExpense_Returns422StatusUnchanged`; `CancelAsync_NonCancellableStatus_ReturnsNotCancellable` (Theory) | Covered |
| BR-06 | Managers cannot approve their own expenses | Integration + Unit | `ExpenseApprovalTests.cs`; `ExpenseServiceTests.cs` | `Approve_OwnExpense_Returns403`, `Reject_OwnExpense_Returns403`; `ApproveAsync_OwnExpense_ReturnsNotAuthorizedReviewer` | Covered |
| BR-07 | Rejected expenses are read-only | Unit | `UnitTests/.../ExpenseServiceTests.cs` | `UpdateAsync_NonEditableStatus_ReturnsNotEditable(Rejected)`, `CancelAsync_NonCancellableStatus_ReturnsNotCancellable(Rejected)`, `ApproveAsync_NonSubmittedStatus_ReturnsNotSubmitted(Rejected)`, `RejectAsync_NonSubmittedStatus_ReturnsNotSubmitted(Rejected)` | Covered |
| BR-08 | Finance can reimburse only approved (or compliance-approved) expenses | Integration | `ExpenseReimbursementTests.cs` | `Reimburse_ApprovedNonClientEntertainment_Returns200Reimbursed`, `Reimburse_ComplianceApprovedClientEntertainment_Returns200Reimbursed`, `Reimburse_NonEligibleStatus_Returns422StatusUnchanged` (Theory) | Covered |
| BR-09 | Expense number shall be generated automatically | Unit + Integration | `ExpenseNumberGeneratorTests.cs`; `ExpenseSubmissionTests.cs` | `GenerateAsync_ReflectsCompanyClockDate`, `GenerateAsync_IncrementsPastExistingSameDayCount`, `CreateAsync_RetriesOnExpenseNumberConflict_ThenSucceeds`; `Create_ClientSuppliedExpenseNumber_IsIgnored` | Covered |
| BR-10 | All dates shall be stored in the company's local timezone | — | — | — | **GAP — flagged for user decision, see note below** |

> **Genuine gap flagged, NOT silently patched (BR-10):** no test asserts that persisted
> `Expense` timestamps (`CreatedAt`, `SubmittedAt`, `ApprovedAt`, `ComplianceApprovedAt`,
> `RejectedAt`, `ReimbursedAt`) are actually stored in company-local time. Tracing the
> implementation: `ExpenseService.cs` writes all of these via raw `DateTime.UtcNow`;
> `ICompanyClock` (the local-timezone abstraction) is only consulted for the BR-02 "is this
> date in the future" comparison and for report date-range bounds in `ReportService.cs` — never
> at the point audit timestamps are actually persisted. `ExpenseDate` itself is a `DateOnly`
> with no time/timezone component, so it's not affected either way. This reads as a genuine
> implementation gap against BR-10's literal wording, not merely a missing test — per
> `proposal.md`'s explicit scope boundary ("if the audit surfaces a genuine functional gap...
> that is a separate defect to be raised... not silently patched inside ET020") this is
> deliberately left unresolved here pending a decision on whether UTC-storage-with-local-display
> is the intended interpretation or an actual BR-10 violation. **Tracked as ET021**
> (`docs/TICKETS.md`) for future resolution — not fixed in ET020 per its verify-only scope.

---

## Frontend UI-Behavior Cross-Check (ET020, task 3.1–3.2)

Per `docs/SDS.md` §10.5 / `AGENTS.md` §10, business rules are verified once on the backend
(fully audited above); frontend Vitest/RTL tests exist only to verify UI-observable behavior
(messages, role-based gating, loading/error states). This section records that cross-check —
not a duplicate of the backend AC/BR rows above.

| UI Behavior | FRS Ref | Test File | Test Method/Describe | Status |
|---|---|---|---|---|
| Required-field messages (expense date, category, amount, description) render on blank/unselected submit | 4.1.1 | `features/expenses/components/ExpenseForm.test.tsx` | `shows a required-field message when expense date is left blank`, `...no category is selected`, `...amount is left blank`, `...description is left blank` (new) | Added (ET020) |
| Receipt attachment required message | 4.1.1, BR-03 | `ExpenseForm.test.tsx` | `blocks submission with no attachment selected` | Covered |
| Currency fixed/disabled to INR in the form | 4.1.1 | `ExpenseForm.test.tsx` | `does not render an editable currency field (fixed to INR)` | Covered |
| Attachment type/size client-side validation messages | 4.1.3, 4.1.4 | `features/expenses/components/AttachmentPicker.test.tsx` | disallowed-type, allowed-type, oversized, at-boundary cases | Covered |
| Edit-route ownership guard (non-owner can't reach `/expenses/{id}/edit`) | ET018 finding | `pages/EditExpensePage.test.tsx`; `features/expenses/components/ExpenseDetail.test.tsx` | `redirects a non-owner to the detail route without rendering the edit form`; `shows no action controls and a read-only note for a non-owner` | Covered |
| Mandatory rejection comment — button disabled AND inline error text rendered | 5.1.4, 5.2.5 | `features/expenses/components/RejectionDialog.test.tsx` | `disables submission when the comment is whitespace only`; `shows the inline required-comment error message once the field is touched` (new) | Added (ET020) |
| Finance-only nav links (Finance Search, Monthly Report) shown/hidden by role | 7.1.1, 10.1.2 | `layouts/AppLayout.test.tsx` | `shows Finance Search and Monthly Report nav links for Finance`; `hides Finance Search and Monthly Report nav links for %s` (new) | Added (ET020) |
| Dashboard shows only the metrics assigned to the caller's role | 8.1.1–8.1.3 | `pages/DashboardPage.test.tsx` | per-role metric-set assertions incl. `queryByText(...).not.toBeInTheDocument()` for withheld metrics | Covered |
| Finance search filters submit each field individually (Expense Number, Employee Name, Date Range, Category, Status) | 7.1.1 | `features/expenses/components/FinanceSearchFilters.test.tsx` | `submits expenseNumber alone...`, `...employeeName alone...`, `...date range`, `submits category alone...` (new), `submits status alone...` (new), `combines category and status...` | Added (ET020) |
| Role/ownership/status-based review-action button visibility (BR-06 etc.) | 5.1.6 etc. | `ExpenseDetail.test.tsx`; `features/expenses/utils/reviewEligibility.test.ts` | extensive per-role/status/category matrix | Covered |

> **UI defect flagged, not silently fixed:** `pages/ExpenseListPage.tsx` renders the "New
> expense" link unconditionally for every role, but `/expenses/new` is route-guarded to
> `Employee`/`Manager` only (`routes/AppRouter.tsx`) — a Finance or ComplianceOfficer user
> would see a clickable button that silently bounces them to `/dashboard` via `RequireRole`.
> Not a security issue (the route guard already blocks it) — a cosmetic/UX inconsistency. Per
> proposal.md's scope boundary (no `frontend/pages` changes in ET020 unless already agreed), not
> fixed here. **Tracked as ET021** (`docs/TICKETS.md`) alongside the two backend findings.

## New E2E Coverage (ET020 gap-fill)

| AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status |
|---|---|---|---|---|---|
| 8.1.1 (e2e) | Employee sees only Total Submitted/Approved/Reimbursed, end-to-end | E2E | `e2e/08-dashboard-reports.spec.ts` | `employee sees only employee-scoped dashboard metrics` | Added (ET020) |
| 8.1.2 (e2e) | Manager sees + Pending Approvals, end-to-end | E2E | `e2e/08-dashboard-reports.spec.ts` | `manager sees manager-scoped dashboard metrics, including Pending Approvals` | Added (ET020) |
| 8.1.3 (e2e) | Finance sees + Pending Reimbursements, end-to-end | E2E | `e2e/08-dashboard-reports.spec.ts` | `finance sees finance-scoped dashboard metrics, including Pending Reimbursements` | Added (ET020) |
| 7.1.4 / 10.1.2 (e2e) | Finance downloads monthly reimbursement report (.xlsx), end-to-end | E2E | `e2e/08-dashboard-reports.spec.ts` | `finance downloads the monthly reimbursement report`; `a non-Finance role cannot reach the monthly report route` | Added (ET020) |
| — (e2e) | Receipt/attachment viewer opens end-to-end (owner) | E2E | `e2e/08-dashboard-reports.spec.ts` | `employee submits an expense with a receipt attachment`; `the expense owner can view the receipt attachment` | Added (ET020) |

**Notes on `e2e/08-dashboard-reports.spec.ts`:** all 7 scenarios pass locally, both standalone and
as part of the full 8-file suite (with a test-only `RateLimiting__AuthEndpoints__PermitLimit`
override — see design.md Risks). Implementing this file surfaced and fixed 3 genuine pre-existing
defects in `e2e/authHelpers.ts`/`02-auth-login.spec.ts`/`04-route-guards.spec.ts` (stale "welcome
text" assertions, a ComplianceOfficer dashboard-redirect bug) that predate ET020 — see design.md
Risks/Trade-offs for full detail on these and on the rate-limit interaction discovered during
implementation.
