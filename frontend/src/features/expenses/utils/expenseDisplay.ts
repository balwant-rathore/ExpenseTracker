import type { ExpenseCategory, ExpenseStatus } from '@/types/expense'

const CATEGORY_LABELS: Record<ExpenseCategory, string> = {
  Travel: 'Travel',
  Hotel: 'Hotel',
  Meals: 'Meals',
  OfficeSupplies: 'Office Supplies',
  ClientEntertainment: 'Client Entertainment',
  Training: 'Training',
  Other: 'Other',
}

const STATUS_LABELS: Record<ExpenseStatus, string> = {
  Draft: 'Draft',
  Submitted: 'Submitted',
  Approved: 'Approved',
  ComplianceApproved: 'Compliance Approved',
  Cancelled: 'Cancelled',
  Reimbursed: 'Reimbursed',
  Rejected: 'Rejected',
}

export function categoryLabel(category: ExpenseCategory): string {
  return CATEGORY_LABELS[category]
}

export function statusLabel(status: ExpenseStatus): string {
  return STATUS_LABELS[status]
}

export function formatCurrency(amount: number, currency: string): string {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency }).format(amount)
}
