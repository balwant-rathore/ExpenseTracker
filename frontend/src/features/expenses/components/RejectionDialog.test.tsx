import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import type { ExpenseEnvelopeResponse } from '@/types/expense'
import { renderWithProviders } from '@/test/renderWithProviders'
import * as expenseApi from '../api/expenseApi'
import { RejectionDialog } from './RejectionDialog'

function makeEnvelope(): ExpenseEnvelopeResponse {
  return {
    expense: {
      id: 'expense-1',
      expenseNumber: 'EXP-20260101-0001',
      expenseDate: '2026-01-01',
      category: 'Travel',
      amount: 100,
      currency: 'INR',
      description: 'Taxi',
      status: 'Rejected',
      submittedAt: null,
      approvedAt: null,
      complianceApprovedAt: null,
      rejectedAt: '2026-01-02T00:00:00Z',
      rejectionComment: 'Needs more detail',
      reimbursedAt: null,
      createdAt: '2026-01-01T00:00:00Z',
      employeeName: 'Ada Lovelace',
      employeeNumber: 'EMP001',
      receiptAttachmentId: 'attachment-1',
      attachmentOriginalFileName: 'receipt.pdf',
    },
  }
}

function openDialog(action: 'managerReject' | 'complianceReject' = 'managerReject') {
  renderWithProviders(<RejectionDialog expenseId="expense-1" action={action} />)
  fireEvent.click(screen.getByRole('button', { name: /^reject$/i }))
}

describe('RejectionDialog', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('disables submission when the comment is empty', () => {
    openDialog()
    expect(screen.getByRole('button', { name: /confirm rejection/i })).toBeDisabled()
  })

  it('disables submission when the comment is whitespace only', async () => {
    openDialog()
    fireEvent.change(screen.getByLabelText(/rejection comment/i), { target: { value: '   ' } })
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /confirm rejection/i })).toBeDisabled(),
    )
  })

  it('calls the reject endpoint when action is managerReject', async () => {
    const rejectSpy = vi.spyOn(expenseApi, 'rejectExpense').mockResolvedValue(makeEnvelope())
    openDialog('managerReject')

    fireEvent.change(screen.getByLabelText(/rejection comment/i), {
      target: { value: 'Needs more detail' },
    })
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /confirm rejection/i })).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: /confirm rejection/i }))

    await waitFor(() =>
      expect(rejectSpy).toHaveBeenCalledWith('expense-1', 'Needs more detail', null),
    )
  })

  it('calls the compliance-reject endpoint when action is complianceReject', async () => {
    const complianceRejectSpy = vi
      .spyOn(expenseApi, 'complianceRejectExpense')
      .mockResolvedValue(makeEnvelope())
    openDialog('complianceReject')

    fireEvent.change(screen.getByLabelText(/rejection comment/i), {
      target: { value: 'Not a valid receipt' },
    })
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /confirm rejection/i })).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: /confirm rejection/i }))

    await waitFor(() =>
      expect(complianceRejectSpy).toHaveBeenCalledWith('expense-1', 'Not a valid receipt', null),
    )
  })

  it('shows the backend error without closing the dialog', async () => {
    vi.spyOn(expenseApi, 'rejectExpense').mockRejectedValue({
      code: 'BUSINESS_RULE_VIOLATION',
      message: 'Only Submitted expenses can be rejected.',
      fields: [],
      traceId: 't',
    })
    openDialog('managerReject')

    fireEvent.change(screen.getByLabelText(/rejection comment/i), {
      target: { value: 'Needs more detail' },
    })
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /confirm rejection/i })).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: /confirm rejection/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Only Submitted expenses can be rejected.')
    expect(screen.getByLabelText(/rejection comment/i)).toBeInTheDocument()
  })
})
