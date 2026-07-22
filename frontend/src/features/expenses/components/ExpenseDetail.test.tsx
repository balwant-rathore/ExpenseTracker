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

  describe('Manager review actions', () => {
    it("sees Approve/Reject on a direct report's Submitted expense", () => {
      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Submitted' })}
          currentUserEmployeeNumber="MANAGER1"
          currentUserRole="Manager"
        />,
      )

      expect(screen.getByRole('button', { name: /^approve$/i })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /^reject$/i })).toBeInTheDocument()
    })

    it('does not see Approve/Reject on their own Submitted expense', () => {
      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Submitted' })}
          currentUserEmployeeNumber="EMP001"
          currentUserRole="Manager"
        />,
      )

      expect(screen.queryByRole('button', { name: /^approve$/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /^reject$/i })).not.toBeInTheDocument()
    })

    it('does not see Approve/Reject on a direct report expense that is not Submitted', () => {
      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Approved' })}
          currentUserEmployeeNumber="MANAGER1"
          currentUserRole="Manager"
        />,
      )

      expect(screen.queryByRole('button', { name: /^approve$/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /^reject$/i })).not.toBeInTheDocument()
    })

    it('calls the approve endpoint and refetches on success', async () => {
      const approveSpy = vi.spyOn(expenseApi, 'approveExpense').mockResolvedValue({
        expense: makeExpense({ status: 'Approved' }),
      })

      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Submitted' })}
          currentUserEmployeeNumber="MANAGER1"
          currentUserRole="Manager"
        />,
      )
      fireEvent.click(screen.getByRole('button', { name: /^approve$/i }))

      await waitFor(() => expect(approveSpy).toHaveBeenCalledWith('expense-1', null))
    })

    it('opens the shared rejection dialog and does not call reject before a valid comment is submitted', () => {
      const rejectSpy = vi.spyOn(expenseApi, 'rejectExpense')

      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Submitted' })}
          currentUserEmployeeNumber="MANAGER1"
          currentUserRole="Manager"
        />,
      )
      fireEvent.click(screen.getByRole('button', { name: /^reject$/i }))

      expect(screen.getByLabelText(/rejection comment/i)).toBeInTheDocument()
      expect(rejectSpy).not.toHaveBeenCalled()
    })

    it('surfaces a 403/422 from approve without a false-success state', async () => {
      vi.spyOn(expenseApi, 'approveExpense').mockRejectedValue({
        code: 'AUTHORIZATION_FAILED',
        message: 'You cannot approve your own expense.',
        fields: [],
        traceId: 't',
      })

      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Submitted' })}
          currentUserEmployeeNumber="MANAGER1"
          currentUserRole="Manager"
        />,
      )
      fireEvent.click(screen.getByRole('button', { name: /^approve$/i }))

      const alert = await screen.findByRole('alert')
      expect(alert).toHaveTextContent('You cannot approve your own expense.')
      expect(screen.getByText(/submitted/i)).toBeInTheDocument()
    })
  })

  describe('Compliance review actions', () => {
    it('sees Approve/Reject on an Approved ClientEntertainment expense', () => {
      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Approved', category: 'ClientEntertainment' })}
          currentUserEmployeeNumber="COMPLIANCE1"
          currentUserRole="ComplianceOfficer"
        />,
      )

      expect(screen.getByRole('button', { name: /^approve$/i })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /^reject$/i })).toBeInTheDocument()
    })

    it('does not see Approve/Reject on a non-ClientEntertainment expense', () => {
      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Approved', category: 'Travel' })}
          currentUserEmployeeNumber="COMPLIANCE1"
          currentUserRole="ComplianceOfficer"
        />,
      )

      expect(screen.queryByRole('button', { name: /^approve$/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /^reject$/i })).not.toBeInTheDocument()
    })

    it('does not see Approve/Reject on a ClientEntertainment expense that is not Approved', () => {
      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Submitted', category: 'ClientEntertainment' })}
          currentUserEmployeeNumber="COMPLIANCE1"
          currentUserRole="ComplianceOfficer"
        />,
      )

      expect(screen.queryByRole('button', { name: /^approve$/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /^reject$/i })).not.toBeInTheDocument()
    })

    it('calls the compliance-approve endpoint and refetches on success', async () => {
      const complianceApproveSpy = vi.spyOn(expenseApi, 'complianceApproveExpense').mockResolvedValue({
        expense: makeExpense({ status: 'ComplianceApproved', category: 'ClientEntertainment' }),
      })

      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Approved', category: 'ClientEntertainment' })}
          currentUserEmployeeNumber="COMPLIANCE1"
          currentUserRole="ComplianceOfficer"
        />,
      )
      fireEvent.click(screen.getByRole('button', { name: /^approve$/i }))

      await waitFor(() => expect(complianceApproveSpy).toHaveBeenCalledWith('expense-1', null))
    })

    it('opens the shared rejection dialog and does not call compliance-reject before a valid comment is submitted', () => {
      const complianceRejectSpy = vi.spyOn(expenseApi, 'complianceRejectExpense')

      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Approved', category: 'ClientEntertainment' })}
          currentUserEmployeeNumber="COMPLIANCE1"
          currentUserRole="ComplianceOfficer"
        />,
      )
      fireEvent.click(screen.getByRole('button', { name: /^reject$/i }))

      expect(screen.getByLabelText(/rejection comment/i)).toBeInTheDocument()
      expect(complianceRejectSpy).not.toHaveBeenCalled()
    })

    it('surfaces a 403/422 from compliance-approve without a false-success state', async () => {
      vi.spyOn(expenseApi, 'complianceApproveExpense').mockRejectedValue({
        code: 'BUSINESS_RULE_VIOLATION',
        message: 'Only Approved Client Entertainment expenses can be compliance-approved.',
        fields: [],
        traceId: 't',
      })

      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Approved', category: 'ClientEntertainment' })}
          currentUserEmployeeNumber="COMPLIANCE1"
          currentUserRole="ComplianceOfficer"
        />,
      )
      fireEvent.click(screen.getByRole('button', { name: /^approve$/i }))

      const alert = await screen.findByRole('alert')
      expect(alert).toHaveTextContent(
        'Only Approved Client Entertainment expenses can be compliance-approved.',
      )
    })
  })

  describe('Finance reimburse action', () => {
    it('sees Reimburse on an Approved non-ClientEntertainment expense', () => {
      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Approved', category: 'Travel' })}
          currentUserEmployeeNumber="FINANCE1"
          currentUserRole="Finance"
        />,
      )

      expect(screen.getByRole('button', { name: /reimburse/i })).toBeInTheDocument()
    })

    it('sees Reimburse on a ComplianceApproved ClientEntertainment expense', () => {
      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'ComplianceApproved', category: 'ClientEntertainment' })}
          currentUserEmployeeNumber="FINANCE1"
          currentUserRole="Finance"
        />,
      )

      expect(screen.getByRole('button', { name: /reimburse/i })).toBeInTheDocument()
    })

    it('does not see Reimburse on an Approved (not yet Compliance Approved) ClientEntertainment expense', () => {
      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Approved', category: 'ClientEntertainment' })}
          currentUserEmployeeNumber="FINANCE1"
          currentUserRole="Finance"
        />,
      )

      expect(screen.queryByRole('button', { name: /reimburse/i })).not.toBeInTheDocument()
    })

    it('calls the reimburse endpoint and refetches on success', async () => {
      const reimburseSpy = vi.spyOn(expenseApi, 'reimburseExpense').mockResolvedValue({
        expense: makeExpense({ status: 'Reimbursed', category: 'Travel' }),
      })

      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Approved', category: 'Travel' })}
          currentUserEmployeeNumber="FINANCE1"
          currentUserRole="Finance"
        />,
      )
      fireEvent.click(screen.getByRole('button', { name: /reimburse/i }))

      await waitFor(() => expect(reimburseSpy).toHaveBeenCalledWith('expense-1', null))
    })

    it('surfaces a 422 from reimburse without a false-success state', async () => {
      vi.spyOn(expenseApi, 'reimburseExpense').mockRejectedValue({
        code: 'BUSINESS_RULE_VIOLATION',
        message: 'Only Approved expenses can be reimbursed.',
        fields: [],
        traceId: 't',
      })

      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Approved', category: 'Travel' })}
          currentUserEmployeeNumber="FINANCE1"
          currentUserRole="Finance"
        />,
      )
      fireEvent.click(screen.getByRole('button', { name: /reimburse/i }))

      const alert = await screen.findByRole('alert')
      expect(alert).toHaveTextContent('Only Approved expenses can be reimbursed.')
    })
  })

  describe('Non-owner action visibility (frontend-expense-visibility-ui)', () => {
    it('owner sees Edit/Submit/Cancel and no review action even when a review role is also passed', () => {
      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Submitted' })}
          currentUserEmployeeNumber="EMP001"
          currentUserRole="Manager"
        />,
      )

      expect(screen.getByRole('link', { name: /edit/i })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /cancel expense/i })).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /^approve$/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /^reject$/i })).not.toBeInTheDocument()
    })

    it('a non-owner with an applicable review role sees that role action and no owner-only action', () => {
      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Submitted' })}
          currentUserEmployeeNumber="MANAGER1"
          currentUserRole="Manager"
        />,
      )

      expect(screen.getByRole('button', { name: /^approve$/i })).toBeInTheDocument()
      expect(screen.queryByRole('link', { name: /edit/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /^submit$/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /cancel expense/i })).not.toBeInTheDocument()
    })

    it('a non-owner with no applicable review role or status sees a read-only view with no action controls', () => {
      renderWithProviders(
        <ExpenseDetail
          expense={makeExpense({ status: 'Approved' })}
          currentUserEmployeeNumber="MANAGER1"
          currentUserRole="Manager"
        />,
      )

      expect(screen.queryByRole('link', { name: /edit/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /^submit$/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /cancel expense/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /^approve$/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /^reject$/i })).not.toBeInTheDocument()
      expect(screen.getByText('This is a read-only view.')).toBeInTheDocument()
    })
  })
})
