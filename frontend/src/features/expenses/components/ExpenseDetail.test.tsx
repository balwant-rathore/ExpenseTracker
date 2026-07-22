import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import type { ExpenseResponse, ExpenseStatus } from '@/types/expense'
import { renderWithProviders } from '@/test/renderWithProviders'
import * as expenseApi from '../api/expenseApi'
import { ExpenseDetail } from './ExpenseDetail'

function makeExpense(overrides: Partial<ExpenseResponse> = {}): ExpenseResponse {
  return {
    id: 'expense-1',
    expenseNumber: 'EXP-20260101-0001',
    expenseDate: '2026-01-01',
    category: 'Travel',
    amount: 100,
    currency: 'INR',
    description: 'Taxi',
    status: 'Draft',
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
    ...overrides,
  }
}

describe('ExpenseDetail', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('shows Edit, Submit, and Cancel actions for the owner of a Draft expense', () => {
    renderWithProviders(
      <ExpenseDetail expense={makeExpense({ status: 'Draft' })} currentUserEmployeeNumber="EMP001" />,
    )

    expect(screen.getByRole('link', { name: /edit/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^submit$/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /cancel expense/i })).toBeInTheDocument()
  })

  it('shows Edit and Cancel (but not Submit) for the owner of a Submitted expense', () => {
    renderWithProviders(
      <ExpenseDetail
        expense={makeExpense({ status: 'Submitted' })}
        currentUserEmployeeNumber="EMP001"
      />,
    )

    expect(screen.getByRole('link', { name: /edit/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /cancel expense/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^submit$/i })).not.toBeInTheDocument()
  })

  it('shows no action controls and a read-only note for a non-owner', () => {
    renderWithProviders(
      <ExpenseDetail
        expense={makeExpense({ status: 'Submitted' })}
        currentUserEmployeeNumber="MANAGER1"
      />,
    )

    expect(screen.queryByRole('link', { name: /edit/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^submit$/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /cancel expense/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /approve/i })).not.toBeInTheDocument()
    expect(screen.getByText('This is a read-only view.')).toBeInTheDocument()
  })

  it.each<ExpenseStatus>(['Approved', 'ComplianceApproved', 'Rejected', 'Cancelled', 'Reimbursed'])(
    'hides Edit and Cancel for a %s expense even for the owner',
    (status) => {
      renderWithProviders(
        <ExpenseDetail expense={makeExpense({ status })} currentUserEmployeeNumber="EMP001" />,
      )
      expect(screen.queryByRole('link', { name: /edit/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /cancel expense/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /^submit$/i })).not.toBeInTheDocument()
    },
  )

  it('calls the submit endpoint when the owner clicks Submit on their own Draft', async () => {
    const submitSpy = vi.spyOn(expenseApi, 'submitExpense').mockResolvedValue({
      expense: makeExpense({ status: 'Submitted' }),
    })

    renderWithProviders(
      <ExpenseDetail expense={makeExpense({ status: 'Draft' })} currentUserEmployeeNumber="EMP001" />,
    )
    fireEvent.click(screen.getByRole('button', { name: /^submit$/i }))

    await waitFor(() => expect(submitSpy).toHaveBeenCalledWith('expense-1', null))
  })

  it('surfaces a backend rejection when submitting fails', async () => {
    vi.spyOn(expenseApi, 'submitExpense').mockRejectedValue({
      code: 'BUSINESS_RULE_VIOLATION',
      message: 'Only expenses in Draft status can be submitted.',
      fields: [],
      traceId: 't',
    })

    renderWithProviders(
      <ExpenseDetail expense={makeExpense({ status: 'Draft' })} currentUserEmployeeNumber="EMP001" />,
    )
    fireEvent.click(screen.getByRole('button', { name: /^submit$/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Only expenses in Draft status can be submitted.')
  })
})
