export type ExpenseCategory =
  | 'Travel'
  | 'Hotel'
  | 'Meals'
  | 'OfficeSupplies'
  | 'ClientEntertainment'
  | 'Training'
  | 'Other'

export const EXPENSE_CATEGORIES: ExpenseCategory[] = [
  'Travel',
  'Hotel',
  'Meals',
  'OfficeSupplies',
  'ClientEntertainment',
  'Training',
  'Other',
]

export type ExpenseStatus =
  | 'Draft'
  | 'Submitted'
  | 'Approved'
  | 'ComplianceApproved'
  | 'Cancelled'
  | 'Reimbursed'
  | 'Rejected'

export const EXPENSE_STATUSES: ExpenseStatus[] = [
  'Draft',
  'Submitted',
  'Approved',
  'ComplianceApproved',
  'Cancelled',
  'Reimbursed',
  'Rejected',
]

export type ExpenseSortField =
  | 'expenseDate'
  | 'expenseNumber'
  | 'createdAt'
  | 'amount'
  | 'submittedAt'
  | 'approvedAt'
  | 'reimbursedAt'
  | 'rejectedAt'

export type ExpenseAction = 'Draft' | 'Submit'

export interface ExpenseResponse {
  id: string
  expenseNumber: string
  expenseDate: string
  category: ExpenseCategory
  amount: number
  currency: 'INR'
  description: string
  status: ExpenseStatus
  submittedAt: string | null
  approvedAt: string | null
  complianceApprovedAt: string | null
  rejectedAt: string | null
  rejectionComment: string | null
  reimbursedAt: string | null
  createdAt: string
  employeeName: string | null
  employeeNumber: string | null
  receiptAttachmentId: string
  attachmentOriginalFileName: string | null
}

export interface ExpenseEnvelopeResponse {
  expense: ExpenseResponse
}

export interface PagedExpenseResponse {
  items: ExpenseResponse[]
  page: number
  pageSize: number
  totalRecords: number
}
