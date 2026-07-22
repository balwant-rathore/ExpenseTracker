import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { selectOption } from '@/test/selectOption'
import type { ExpenseResponse } from '@/types/expense'
import { ExpenseList } from './ExpenseList'

function makeExpense(overrides: Partial<ExpenseResponse> = {}): ExpenseResponse {
  return {
    id: 'expense-1',
    expenseNumber: 'EXP-20260101-0001',
    expenseDate: '2026-01-01',
    category: 'Travel',
    amount: 100,
    currency: 'INR',
    description: 'Taxi',
    status: 'Submitted',
    submittedAt: '2026-01-01T00:00:00Z',
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

const defaultProps = {
  items: [makeExpense()],
  page: 1,
  pageSize: 20 as const,
  totalRecords: 1,
  sortBy: 'expenseDate' as const,
  sortDirection: 'desc' as const,
  onPageChange: vi.fn(),
  onPageSizeChange: vi.fn(),
  onSortByChange: vi.fn(),
  onSortDirectionChange: vi.fn(),
}

function renderList(props: Partial<React.ComponentProps<typeof ExpenseList>> = {}) {
  return render(
    <MemoryRouter>
      <ExpenseList {...defaultProps} {...props} />
    </MemoryRouter>,
  )
}

describe('ExpenseList', () => {
  it('renders every provided item exactly as given, with pagination info', () => {
    renderList({
      items: [makeExpense({ id: 'e1', expenseNumber: 'EXP-1' }), makeExpense({ id: 'e2', expenseNumber: 'EXP-2' })],
      totalRecords: 2,
    })

    expect(screen.getByText('EXP-1')).toBeInTheDocument()
    expect(screen.getByText('EXP-2')).toBeInTheDocument()
    expect(screen.getByText(/page 1 of 1/i)).toBeInTheDocument()
  })

  it("shows a Mine badge for the current user's own row", () => {
    renderList({
      items: [makeExpense({ employeeNumber: 'EMP001' })],
      currentUserEmployeeNumber: 'EMP001',
    })
    expect(screen.getByText('Mine')).toBeInTheDocument()
  })

  it("does not show a Mine badge for a direct report's row", () => {
    renderList({
      items: [makeExpense({ employeeNumber: 'EMP002' })],
      currentUserEmployeeNumber: 'EMP001',
    })
    expect(screen.queryByText('Mine')).not.toBeInTheDocument()
  })

  it('calls onPageSizeChange when the page size selection changes', async () => {
    const onPageSizeChange = vi.fn()
    renderList({ onPageSizeChange })

    await selectOption('Page size', '50')

    expect(onPageSizeChange).toHaveBeenCalledWith(50)
  })

  it('calls onSortByChange when the sort field selection changes', async () => {
    const onSortByChange = vi.fn()
    renderList({ onSortByChange })

    await selectOption('Sort by', 'Amount')

    expect(onSortByChange).toHaveBeenCalledWith('amount')
  })

  it('never shows approve/reject controls', () => {
    renderList()
    expect(screen.queryByRole('button', { name: /approve/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /reject/i })).not.toBeInTheDocument()
  })
})
