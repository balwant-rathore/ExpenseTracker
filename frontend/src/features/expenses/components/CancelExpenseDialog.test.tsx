import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '@/test/renderWithProviders'
import { useAuthStore } from '@/store/authStore'
import * as expenseApi from '../api/expenseApi'
import { CancelExpenseDialog } from './CancelExpenseDialog'

describe('CancelExpenseDialog', () => {
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

  it('requires an explicit confirmation step before calling the cancel endpoint', async () => {
    const cancelSpy = vi.spyOn(expenseApi, 'cancelExpense')

    renderWithProviders(<CancelExpenseDialog expenseId="expense-1" />)
    fireEvent.click(screen.getByRole('button', { name: /cancel expense/i }))

    expect(await screen.findByText('Cancel this expense?')).toBeInTheDocument()
    expect(cancelSpy).not.toHaveBeenCalled()
  })

  it('calls the cancel endpoint only after the confirmation is clicked', async () => {
    const cancelSpy = vi.spyOn(expenseApi, 'cancelExpense').mockResolvedValue({
      expense: {
        id: 'expense-1',
        expenseNumber: 'EXP-20260101-0001',
        expenseDate: '2026-01-01',
        category: 'Travel',
        amount: 100,
        currency: 'INR',
        description: 'x',
        status: 'Cancelled',
        submittedAt: null,
        approvedAt: null,
        complianceApprovedAt: null,
        rejectedAt: null,
        rejectionComment: null,
        reimbursedAt: null,
        createdAt: '2026-01-01T00:00:00Z',
        employeeName: 'Ada Lovelace',
        employeeNumber: 'EMP001',
        receiptAttachmentId: 'attachment-1',
        attachmentOriginalFileName: 'receipt.pdf',
      },
    })

    renderWithProviders(<CancelExpenseDialog expenseId="expense-1" />)
    fireEvent.click(screen.getByRole('button', { name: /cancel expense/i }))
    fireEvent.click(await screen.findByRole('button', { name: /yes, cancel it/i }))

    await waitFor(() => expect(cancelSpy).toHaveBeenCalledWith('expense-1', 'token'))
  })
})
