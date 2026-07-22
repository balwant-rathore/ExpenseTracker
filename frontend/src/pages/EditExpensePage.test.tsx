import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import type { ExpenseResponse } from '@/types/expense'
import * as expenseApi from '@/features/expenses/api/expenseApi'
import { EditExpensePage } from './EditExpensePage'

function renderAt(id: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/expenses/${id}/edit`]}>
        <Routes>
          <Route path="/expenses/:id/edit" element={<EditExpensePage />} />
          <Route path="/expenses/:id" element={<div>Detail page</div>} />
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
  status: 'Draft',
  submittedAt: null,
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

function setUser(employeeNumber: string) {
  useAuthStore.setState({
    user: {
      id: 'employee-1',
      email: 'a@b.com',
      employeeNumber,
      firstName: 'Ada',
      lastName: 'Lovelace',
      role: 'Employee',
    },
    accessToken: 'token',
    status: 'authenticated',
  })
}

describe('EditExpensePage', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('renders the pre-filled edit form for the owner', async () => {
    setUser('EMP1')
    vi.spyOn(expenseApi, 'getExpense').mockResolvedValue({ expense })

    renderAt('expense-1')

    expect(await screen.findByDisplayValue('2026-01-01')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Taxi fare')).toBeInTheDocument()
  })

  it('calls the update endpoint when the owner submits a valid edit', async () => {
    setUser('EMP1')
    vi.spyOn(expenseApi, 'getExpense').mockResolvedValue({ expense })
    const updateSpy = vi
      .spyOn(expenseApi, 'updateExpense')
      .mockResolvedValue({ expense: { ...expense, description: 'Updated' } })

    renderAt('expense-1')
    await screen.findByDisplayValue('Taxi fare')

    fireEvent.change(screen.getByDisplayValue('Taxi fare'), { target: { value: 'Updated' } })
    fireEvent.click(screen.getByRole('button', { name: /save changes/i }))

    await waitFor(() => expect(updateSpy).toHaveBeenCalled())
    expect(updateSpy.mock.calls[0][0]).toBe('expense-1')
  })

  it('does not render any Status control on the edit form', async () => {
    setUser('EMP1')
    vi.spyOn(expenseApi, 'getExpense').mockResolvedValue({ expense })

    renderAt('expense-1')
    await screen.findByDisplayValue('Taxi fare')

    expect(screen.queryByLabelText(/status/i)).not.toBeInTheDocument()
  })

  it('redirects a non-owner to the detail route without rendering the edit form', async () => {
    setUser('MANAGER1')
    vi.spyOn(expenseApi, 'getExpense').mockResolvedValue({ expense })

    renderAt('expense-1')

    expect(await screen.findByText('Detail page')).toBeInTheDocument()
    expect(screen.queryByDisplayValue('Taxi fare')).not.toBeInTheDocument()
  })
})
