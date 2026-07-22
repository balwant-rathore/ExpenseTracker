import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import type { EmployeeRole } from '@/types/auth'
import type { ExpenseResponse } from '@/types/expense'
import { categoryLabel, formatCurrency, statusLabel } from '../utils/expenseDisplay'
import { getApplicableReviewActions, isExpenseOwner } from '../utils/reviewEligibility'
import { ViewReceiptLink } from './ViewReceiptLink'
import { useSubmitExpense } from '../api/useSubmitExpense'
import { useApproveExpense } from '../api/useApproveExpense'
import { useComplianceApprove } from '../api/useComplianceApprove'
import { useReimburseExpense } from '../api/useReimburseExpense'
import { CancelExpenseDialog } from './CancelExpenseDialog'
import { RejectionDialog } from './RejectionDialog'

interface ExpenseDetailProps {
  expense: ExpenseResponse
  /**
   * The authenticated user's employeeNumber — not their User id. `/api/auth/me`
   * (and login/register) never expose the Employee GUID, only employeeNumber,
   * so that's the only field the frontend can compare against
   * `ExpenseResponse.employeeNumber` to determine ownership.
   */
  currentUserEmployeeNumber?: string
  currentUserRole?: EmployeeRole
}

export function ExpenseDetail({
  expense,
  currentUserEmployeeNumber,
  currentUserRole,
}: ExpenseDetailProps) {
  const isOwner = isExpenseOwner(expense, currentUserEmployeeNumber)
  const isEditableStatus = expense.status === 'Draft' || expense.status === 'Submitted'
  const canEdit = isOwner && isEditableStatus
  const canSubmit = isOwner && expense.status === 'Draft'
  const canCancel = isOwner && isEditableStatus
  const hasOwnerAction = canEdit || canSubmit || canCancel

  const reviewActions = getApplicableReviewActions(expense, currentUserRole, isOwner)
  const hasManagerAction = reviewActions.some((action) => action.type === 'managerApprove')
  const hasComplianceAction = reviewActions.some((action) => action.type === 'complianceApprove')
  const hasReimburseAction = reviewActions.some((action) => action.type === 'financeReimburse')
  const hasReviewAction = reviewActions.length > 0

  const submitMutation = useSubmitExpense(expense.id)
  const approveMutation = useApproveExpense(expense.id)
  const complianceApproveMutation = useComplianceApprove(expense.id)
  const reimburseMutation = useReimburseExpense(expense.id)

  return (
    <div className="flex flex-col gap-4">
      <dl className="grid grid-cols-2 gap-2">
        <dt className="text-sm text-muted-foreground">Expense number</dt>
        <dd>{expense.expenseNumber}</dd>
        <dt className="text-sm text-muted-foreground">Employee</dt>
        <dd>{expense.employeeName}</dd>
        <dt className="text-sm text-muted-foreground">Category</dt>
        <dd>{categoryLabel(expense.category)}</dd>
        <dt className="text-sm text-muted-foreground">Amount</dt>
        <dd>{formatCurrency(expense.amount, expense.currency)}</dd>
        <dt className="text-sm text-muted-foreground">Status</dt>
        <dd>{statusLabel(expense.status)}</dd>
        <dt className="text-sm text-muted-foreground">Expense date</dt>
        <dd>{expense.expenseDate}</dd>
        <dt className="text-sm text-muted-foreground">Description</dt>
        <dd>{expense.description}</dd>
        <dt className="text-sm text-muted-foreground">Receipt</dt>
        <dd>
          <ViewReceiptLink
            attachmentId={expense.receiptAttachmentId}
            fileName={expense.attachmentOriginalFileName ?? 'Receipt attached'}
          />
        </dd>
        {expense.rejectionComment && (
          <>
            <dt className="text-sm text-muted-foreground">Rejection comment</dt>
            <dd>{expense.rejectionComment}</dd>
          </>
        )}
      </dl>

      {!isOwner && !hasReviewAction && (
        <p className="text-sm text-muted-foreground">This is a read-only view.</p>
      )}

      {hasOwnerAction && (
        <div className="flex flex-wrap gap-2">
          {canEdit && (
            <Button variant="outline" render={<Link to={`/expenses/${expense.id}/edit`} />}>
              Edit
            </Button>
          )}
          {canSubmit && (
            <Button onClick={() => submitMutation.mutate()} disabled={submitMutation.isPending}>
              {submitMutation.isPending ? 'Submitting…' : 'Submit'}
            </Button>
          )}
          {canCancel && <CancelExpenseDialog expenseId={expense.id} />}
        </div>
      )}

      {hasReviewAction && (
        <div className="flex flex-wrap gap-2">
          {hasManagerAction && (
            <>
              <Button onClick={() => approveMutation.mutate()} disabled={approveMutation.isPending}>
                {approveMutation.isPending ? 'Approving…' : 'Approve'}
              </Button>
              <RejectionDialog expenseId={expense.id} action="managerReject" />
            </>
          )}
          {hasComplianceAction && (
            <>
              <Button
                onClick={() => complianceApproveMutation.mutate()}
                disabled={complianceApproveMutation.isPending}
              >
                {complianceApproveMutation.isPending ? 'Approving…' : 'Approve'}
              </Button>
              <RejectionDialog expenseId={expense.id} action="complianceReject" />
            </>
          )}
          {hasReimburseAction && (
            <Button
              onClick={() => reimburseMutation.mutate()}
              disabled={reimburseMutation.isPending}
            >
              {reimburseMutation.isPending ? 'Reimbursing…' : 'Reimburse'}
            </Button>
          )}
        </div>
      )}

      {submitMutation.error && (
        <p role="alert" className="text-sm text-destructive">
          {submitMutation.error.message}
        </p>
      )}
      {approveMutation.error && (
        <p role="alert" className="text-sm text-destructive">
          {approveMutation.error.message}
        </p>
      )}
      {complianceApproveMutation.error && (
        <p role="alert" className="text-sm text-destructive">
          {complianceApproveMutation.error.message}
        </p>
      )}
      {reimburseMutation.error && (
        <p role="alert" className="text-sm text-destructive">
          {reimburseMutation.error.message}
        </p>
      )}
    </div>
  )
}
