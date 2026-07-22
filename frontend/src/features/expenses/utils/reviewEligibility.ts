import type { EmployeeRole } from '@/types/auth'
import type { ExpenseResponse } from '@/types/expense'

export function isExpenseOwner(
  expense: Pick<ExpenseResponse, 'employeeNumber'>,
  currentUserEmployeeNumber: string | undefined,
): boolean {
  return !!currentUserEmployeeNumber && expense.employeeNumber === currentUserEmployeeNumber
}

export type ReviewAction =
  | { type: 'managerApprove' | 'managerReject' }
  | { type: 'complianceApprove' | 'complianceReject' }
  | { type: 'financeReimburse' }

export function getApplicableReviewActions(
  expense: Pick<ExpenseResponse, 'status' | 'category'>,
  role: EmployeeRole | undefined,
  isOwner: boolean,
): ReviewAction[] {
  if (isOwner) {
    return []
  }

  if (role === 'Manager' && expense.status === 'Submitted') {
    return [{ type: 'managerApprove' }, { type: 'managerReject' }]
  }

  if (
    role === 'ComplianceOfficer' &&
    expense.category === 'ClientEntertainment' &&
    expense.status === 'Approved'
  ) {
    return [{ type: 'complianceApprove' }, { type: 'complianceReject' }]
  }

  if (
    role === 'Finance' &&
    ((expense.category !== 'ClientEntertainment' && expense.status === 'Approved') ||
      (expense.category === 'ClientEntertainment' && expense.status === 'ComplianceApproved'))
  ) {
    return [{ type: 'financeReimburse' }]
  }

  return []
}
