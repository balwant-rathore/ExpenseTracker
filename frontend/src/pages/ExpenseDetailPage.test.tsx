import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import type { ExpenseResponse } from '@/types/expense'
import * as expenseApi from '@/features/expenses/api/expenseApi'
import { ExpenseDetailPage } from './ExpenseDetailPage'

function renderAt(id: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/expenses/${id}`]}>
        <Routes>
          <Route path="/expenses/:id" element={<ExpenseDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const expense: ExpenseResponse = {
  id: 'expense-1',
  expenseNumber: 'EXP-20260101-0001',
  expenseDate: '2026-01-01',
  category: 'Travel',
  amount: 100,
  currency: 'INR',
  description: 'Taxi fare',
  status: 'Submitted',
  submittedAt: '2026-01-01T00:00:00Z',
  approvedAt: null,
  complianceApprovedAt: null,
  rejectedAt: null,
  rejectionComment: null,
  reimbursedAt: null,
  createdAt: '2026-01-01T00:00:00Z',
  employeeName: 'Ada Lovelace',
  employeeNumber: 'EMP1',
  receiptAttachmentId: 'attachment-1',
  attachmentOriginalFileName: 'receipt.pdf',
}

describe('ExpenseDetailPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    useAuthStore.setState({
      user: {
        id: 'employee-1',
        email: 'a@b.com',
        employeeNumber: 'EMP1',
        firstName: 'Ada',
        lastName: 'Lovelace',
        role: 'Employee',
      },
      accessToken: 'token',
      status: 'authenticated',
    })
  })

  it('renders the full expense data for an authorized viewer', async () => {
    vi.spyOn(expenseApi, 'getExpense').mockResolvedValue({ expense })

    renderAt('expense-1')

    expect(await screen.findByText('EXP-20260101-0001')).toBeInTheDocument()
    expect(screen.getByText('Taxi fare')).toBeInTheDocument()
  })

  it('renders a not-found state for a nonexistent expense', async () => {
    vi.spyOn(expenseApi, 'getExpense').mockRejectedValue({
      code: 'RESOURCE_NOT_FOUND',
      message: 'Expense not found.',
      fields: [],
      traceId: 't',
    })

    renderAt('missing-id')

    expect(await screen.findByText('This expense could not be found.')).toBeInTheDocument()
  })

  it('renders an access-denied state for an unauthorized viewer', async () => {
    vi.spyOn(expenseApi, 'getExpense').mockRejectedValue({
      code: 'AUTHORIZATION_FAILED',
      message: 'You do not have permission to perform this action.',
      fields: [],
      traceId: 't',
    })

    renderAt('other-expense')

    expect(
      await screen.findByText('You do not have permission to view this expense.'),
    ).toBeInTheDocument()
  })
})
