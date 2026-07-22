import { describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import type { ExpenseResponse } from '@/types/expense'
import { renderWithProviders } from '@/test/renderWithProviders'
import { FinanceSearchResults } from './FinanceSearchResults'

function makeExpense(overrides: Partial<ExpenseResponse> = {}): ExpenseResponse {
  return {
    id: 'expense-1',
    expenseNumber: 'EXP-20260101-0001',
    expenseDate: '2026-01-01',
    category: 'Travel',
    amount: 100,
    currency: 'INR',
    description: 'Taxi',
    status: 'Approved',
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

const noop = vi.fn()

describe('FinanceSearchResults', () => {
  it('renders items, page, pageSize, and totalRecords', () => {
    renderWithProviders(
      <FinanceSearchResults
        items={[makeExpense()]}
        page={1}
        pageSize={20}
        totalRecords={1}
        sortBy="expenseDate"
        sortDirection="desc"
        onPageChange={noop}
        onPageSizeChange={noop}
        onSortByChange={noop}
        onSortDirectionChange={noop}
      />,
    )

    expect(screen.getByText('EXP-20260101-0001')).toBeInTheDocument()
    expect(screen.getByText(/page 1 of 1/i)).toBeInTheDocument()
    expect(screen.getByText(/1 total/i)).toBeInTheDocument()
  })

  it('links each row to the expense detail route', () => {
    renderWithProviders(
      <FinanceSearchResults
        items={[makeExpense()]}
        page={1}
        pageSize={20}
        totalRecords={1}
        sortBy="expenseDate"
        sortDirection="desc"
        onPageChange={noop}
        onPageSizeChange={noop}
        onSortByChange={noop}
        onSortDirectionChange={noop}
      />,
    )

    expect(screen.getByRole('link', { name: 'EXP-20260101-0001' })).toHaveAttribute(
      'href',
      '/expenses/expense-1',
    )
  })

  it('renders no inline reimburse control', () => {
    renderWithProviders(
      <FinanceSearchResults
        items={[makeExpense({ status: 'Approved' })]}
        page={1}
        pageSize={20}
        totalRecords={1}
        sortBy="expenseDate"
        sortDirection="desc"
        onPageChange={noop}
        onPageSizeChange={noop}
        onSortByChange={noop}
        onSortDirectionChange={noop}
      />,
    )

    expect(screen.queryByRole('button', { name: /reimburse/i })).not.toBeInTheDocument()
  })
})
