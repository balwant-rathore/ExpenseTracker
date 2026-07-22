import { apiRequest } from '@/lib/apiClient'
import type {
  ExpenseCategory,
  ExpenseEnvelopeResponse,
  ExpenseSortField,
  ExpenseStatus,
  PagedExpenseResponse,
} from '@/types/expense'

export interface ExpenseFormInput {
  expenseDate: string
  category: ExpenseCategory
  amount: number
  currency: 'INR'
  description: string
  receiptAttachmentId: string
}

export interface CreateExpenseInput extends ExpenseFormInput {
  action: 'Draft' | 'Submit'
}

export interface ExpenseListParams {
  page?: number
  pageSize?: 10 | 20 | 50 | 100
  sortBy?: ExpenseSortField
  sortDirection?: 'asc' | 'desc'
  status?: ExpenseStatus
}

function buildListQuery(params: ExpenseListParams): string {
  const query = new URLSearchParams()
  if (params.page !== undefined) query.set('page', String(params.page))
  if (params.pageSize !== undefined) query.set('pageSize', String(params.pageSize))
  if (params.sortBy !== undefined) query.set('sortBy', params.sortBy)
  if (params.sortDirection !== undefined) query.set('sortDirection', params.sortDirection)
  if (params.status !== undefined) query.set('status', params.status)
  const queryString = query.toString()
  return queryString ? `?${queryString}` : ''
}

export function createExpense(
  input: CreateExpenseInput,
  accessToken?: string | null,
): Promise<ExpenseEnvelopeResponse> {
  return apiRequest<ExpenseEnvelopeResponse>('/expenses', { method: 'POST', body: input, accessToken })
}

export function updateExpense(
  id: string,
  input: ExpenseFormInput,
  accessToken?: string | null,
): Promise<ExpenseEnvelopeResponse> {
  return apiRequest<ExpenseEnvelopeResponse>(`/expenses/${id}`, {
    method: 'PUT',
    body: input,
    accessToken,
  })
}

export function submitExpense(id: string, accessToken?: string | null): Promise<ExpenseEnvelopeResponse> {
  return apiRequest<ExpenseEnvelopeResponse>(`/expenses/${id}/submit`, {
    method: 'POST',
    accessToken,
  })
}

export function cancelExpense(id: string, accessToken?: string | null): Promise<ExpenseEnvelopeResponse> {
  return apiRequest<ExpenseEnvelopeResponse>(`/expenses/${id}/cancel`, {
    method: 'POST',
    accessToken,
  })
}

export function getExpense(id: string, accessToken?: string | null): Promise<ExpenseEnvelopeResponse> {
  return apiRequest<ExpenseEnvelopeResponse>(`/expenses/${id}`, { accessToken })
}

export function listExpenses(
  params: ExpenseListParams,
  accessToken?: string | null,
): Promise<PagedExpenseResponse> {
  return apiRequest<PagedExpenseResponse>(`/expenses${buildListQuery(params)}`, { accessToken })
}
