import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import type { ExpenseResponse } from '@/types/expense'
import { categoryLabel, formatCurrency, statusLabel } from '../utils/expenseDisplay'
import { useSubmitExpense } from '../api/useSubmitExpense'
import { CancelExpenseDialog } from './CancelExpenseDialog'

interface ExpenseDetailProps {
  expense: ExpenseResponse
  /**
   * The authenticated user's employeeNumber — not their User id. `/api/auth/me`
   * (and login/register) never expose the Employee GUID, only employeeNumber,
   * so that's the only field the frontend can compare against
   * `ExpenseResponse.employeeNumber` to determine ownership.
   */
  currentUserEmployeeNumber?: string
}

export function ExpenseDetail({ expense, currentUserEmployeeNumber }: ExpenseDetailProps) {
  const isOwner =
    !!currentUserEmployeeNumber && expense.employeeNumber === currentUserEmployeeNumber
  const isEditableStatus = expense.status === 'Draft' || expense.status === 'Submitted'
  const canEdit = isOwner && isEditableStatus
  const canSubmit = isOwner && expense.status === 'Draft'
  const canCancel = isOwner && isEditableStatus

  const submitMutation = useSubmitExpense(expense.id)

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
        <dd>{expense.attachmentOriginalFileName ?? 'Receipt attached'}</dd>
        {expense.rejectionComment && (
          <>
            <dt className="text-sm text-muted-foreground">Rejection comment</dt>
            <dd>{expense.rejectionComment}</dd>
          </>
        )}
      </dl>

      {!isOwner && <p className="text-sm text-muted-foreground">This is a read-only view.</p>}

      {(canEdit || canSubmit || canCancel) && (
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

      {submitMutation.error && (
        <p role="alert" className="text-sm text-destructive">
          {submitMutation.error.message}
        </p>
      )}
    </div>
  )
}
