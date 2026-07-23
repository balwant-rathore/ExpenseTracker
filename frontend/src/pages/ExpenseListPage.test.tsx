import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '@/test/renderWithProviders'
import { selectOption } from '@/test/selectOption'
import { useAuthStore } from '@/store/authStore'
import type { ExpenseResponse, PagedExpenseResponse } from '@/types/expense'
import * as expenseApi from '@/features/expenses/api/expenseApi'
import { ExpenseListPage } from './ExpenseListPage'

function makeExpense(overrides: Partial<ExpenseResponse> = {}): ExpenseResponse {
  return {
    id: 'e1',
    expenseNumber: 'EXP-1',
    expenseDate: '2026-01-15',
    category: 'Travel',
    amount: 100,
    currency: 'INR',
    description: 'x',
    status: 'Submitted',
    submittedAt: '2026-01-15T00:00:00Z',
    approvedAt: null,
    complianceApprovedAt: null,
    rejectedAt: null,
    rejectionComment: null,
    reimbursedAt: null,
    createdAt: '2026-01-15T00:00:00Z',
    employeeName: 'Ada Lovelace',
    employeeNumber: 'EMP002',
    receiptAttachmentId: 'a1',
    attachmentOriginalFileName: 'r.pdf',
    ...overrides,
  }
}

function pagedResponse(items: ExpenseResponse[]): PagedExpenseResponse {
  return { items, page: 1, pageSize: 20, totalRecords: items.length }
}

describe('ExpenseListPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    useAuthStore.setState({
      user: {
        id: 'manager-1',
        email: 'm@b.com',
        employeeNumber: 'EMP1',
        firstName: 'Mo',
        lastName: 'Ross',
        role: 'Manager',
      },
      accessToken: 'token',
      status: 'authenticated',
    })
  })

  it('shows the New expense link for an Employee', async () => {
    vi.spyOn(expenseApi, 'listExpenses').mockResolvedValue(pagedResponse([]))
    useAuthStore.setState({
      user: {
        id: 'employee-1',
        email: 'e@b.com',
        employeeNumber: 'EMP1',
        firstName: 'Em',
        lastName: 'Ployee',
        role: 'Employee',
      },
      accessToken: 'token',
      status: 'authenticated',
    })

    renderWithProviders(<ExpenseListPage />)

    expect(await screen.findByRole('link', { name: 'New expense' })).toBeInTheDocument()
  })

  it('shows the New expense link for a Manager', async () => {
    vi.spyOn(expenseApi, 'listExpenses').mockResolvedValue(pagedResponse([]))

    renderWithProviders(<ExpenseListPage />)

    expect(await screen.findByRole('link', { name: 'New expense' })).toBeInTheDocument()
  })

  it('hides the New expense link for Finance', async () => {
    vi.spyOn(expenseApi, 'listExpenses').mockResolvedValue(pagedResponse([]))
    useAuthStore.setState({
      user: {
        id: 'finance-1',
        email: 'f@b.com',
        employeeNumber: 'EMP1',
        firstName: 'Fin',
        lastName: 'Ance',
        role: 'Finance',
      },
      accessToken: 'token',
      status: 'authenticated',
    })

    renderWithProviders(<ExpenseListPage />)
    await waitFor(() => expect(expenseApi.listExpenses).toHaveBeenCalled())

    expect(screen.queryByRole('link', { name: 'New expense' })).not.toBeInTheDocument()
  })

  it('hides the New expense link for a Compliance Officer', async () => {
    vi.spyOn(expenseApi, 'listExpenses').mockResolvedValue(pagedResponse([]))
    useAuthStore.setState({
      user: {
        id: 'compliance-1',
        email: 'c@b.com',
        employeeNumber: 'EMP1',
        firstName: 'Comp',
        lastName: 'Liance',
        role: 'ComplianceOfficer',
      },
      accessToken: 'token',
      status: 'authenticated',
    })

    renderWithProviders(<ExpenseListPage />)
    await waitFor(() => expect(expenseApi.listExpenses).toHaveBeenCalled())

    expect(screen.queryByRole('link', { name: 'New expense' })).not.toBeInTheDocument()
  })

  it("renders exactly the backend's returned set for a paged result", async () => {
    vi.spyOn(expenseApi, 'listExpenses').mockResolvedValue(
      pagedResponse([makeExpense({ id: 'e1', expenseNumber: 'EXP-1' })]),
    )

    renderWithProviders(<ExpenseListPage />)

    expect(await screen.findByText('EXP-1')).toBeInTheDocument()
  })

  it("includes the manager's own expenses and their direct reports' expenses in the same list", async () => {
    vi.spyOn(expenseApi, 'listExpenses').mockResolvedValue(
      pagedResponse([
        makeExpense({ id: 'own', expenseNumber: 'EXP-OWN', employeeNumber: 'EMP1', employeeName: 'Mo Ross' }),
        makeExpense({ id: 'report', expenseNumber: 'EXP-REPORT', employeeNumber: 'EMP002', employeeName: 'Ada Lovelace' }),
      ]),
    )

    renderWithProviders(<ExpenseListPage />)

    expect(await screen.findByText('EXP-OWN')).toBeInTheDocument()
    expect(screen.getByText('EXP-REPORT')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /approve/i })).not.toBeInTheDocument()
  })

  it('narrows to the current page when a category filter is applied', async () => {
    vi.spyOn(expenseApi, 'listExpenses').mockResolvedValue(
      pagedResponse([
        makeExpense({ id: 'travel', expenseNumber: 'EXP-TRAVEL', category: 'Travel' }),
        makeExpense({ id: 'meals', expenseNumber: 'EXP-MEALS', category: 'Meals' }),
      ]),
    )

    renderWithProviders(<ExpenseListPage />)
    await screen.findByText('EXP-TRAVEL')

    await selectOption('Category (current page only)', 'Travel')

    expect(screen.getByText('EXP-TRAVEL')).toBeInTheDocument()
    expect(screen.queryByText('EXP-MEALS')).not.toBeInTheDocument()
  })

  it('narrows to the current page when a date range filter is applied', async () => {
    vi.spyOn(expenseApi, 'listExpenses').mockResolvedValue(
      pagedResponse([
        makeExpense({ id: 'jan', expenseNumber: 'EXP-JAN', expenseDate: '2026-01-15' }),
        makeExpense({ id: 'feb', expenseNumber: 'EXP-FEB', expenseDate: '2026-02-15' }),
      ]),
    )

    renderWithProviders(<ExpenseListPage />)
    await screen.findByText('EXP-JAN')

    fireEvent.change(screen.getByLabelText('From date (current page only)'), {
      target: { value: '2026-01-01' },
    })
    fireEvent.change(screen.getByLabelText('To date (current page only)'), {
      target: { value: '2026-01-31' },
    })

    await waitFor(() => expect(screen.queryByText('EXP-FEB')).not.toBeInTheDocument())
    expect(screen.getByText('EXP-JAN')).toBeInTheDocument()
  })

  it('narrows within the current page when category and date filters are combined', async () => {
    vi.spyOn(expenseApi, 'listExpenses').mockResolvedValue(
      pagedResponse([
        makeExpense({ id: 'match', expenseNumber: 'EXP-MATCH', category: 'Travel', expenseDate: '2026-01-15' }),
        makeExpense({ id: 'wrong-cat', expenseNumber: 'EXP-WRONG-CAT', category: 'Meals', expenseDate: '2026-01-15' }),
        makeExpense({ id: 'wrong-date', expenseNumber: 'EXP-WRONG-DATE', category: 'Travel', expenseDate: '2026-03-01' }),
      ]),
    )

    renderWithProviders(<ExpenseListPage />)
    await screen.findByText('EXP-MATCH')

    await selectOption('Category (current page only)', 'Travel')
    fireEvent.change(screen.getByLabelText('From date (current page only)'), {
      target: { value: '2026-01-01' },
    })
    fireEvent.change(screen.getByLabelText('To date (current page only)'), {
      target: { value: '2026-01-31' },
    })

    await waitFor(() => {
      expect(screen.getByText('EXP-MATCH')).toBeInTheDocument()
      expect(screen.queryByText('EXP-WRONG-CAT')).not.toBeInTheDocument()
      expect(screen.queryByText('EXP-WRONG-DATE')).not.toBeInTheDocument()
    })
  })

  it('selecting a status filter calls the list endpoint with that status', async () => {
    const listSpy = vi.spyOn(expenseApi, 'listExpenses').mockResolvedValue(pagedResponse([]))

    renderWithProviders(<ExpenseListPage />)
    await waitFor(() => expect(listSpy).toHaveBeenCalled())

    await selectOption('Status', 'Submitted')

    await waitFor(() =>
      expect(listSpy).toHaveBeenCalledWith(expect.objectContaining({ status: 'Submitted' }), 'token'),
    )
  })

  it('changing the page size refetches the list with the new page size', async () => {
    const listSpy = vi.spyOn(expenseApi, 'listExpenses').mockResolvedValue(pagedResponse([]))

    renderWithProviders(<ExpenseListPage />)
    await waitFor(() => expect(listSpy).toHaveBeenCalledWith(expect.objectContaining({ pageSize: 20 }), 'token'))
    await screen.findByLabelText('Page size')

    await selectOption('Page size', '50')

    await waitFor(() =>
      expect(listSpy).toHaveBeenCalledWith(expect.objectContaining({ pageSize: 50 }), 'token'),
    )
  })

  it('changing the sort field refetches the list with the new sort field', async () => {
    const listSpy = vi.spyOn(expenseApi, 'listExpenses').mockResolvedValue(pagedResponse([]))

    renderWithProviders(<ExpenseListPage />)
    await waitFor(() =>
      expect(listSpy).toHaveBeenCalledWith(expect.objectContaining({ sortBy: 'expenseDate' }), 'token'),
    )
    await screen.findByLabelText('Sort by')

    await selectOption('Sort by', 'Amount')

    await waitFor(() =>
      expect(listSpy).toHaveBeenCalledWith(expect.objectContaining({ sortBy: 'amount' }), 'token'),
    )
  })
})
