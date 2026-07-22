import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import type { PagedExpenseResponse } from '@/types/expense'
import { renderWithProviders } from '@/test/renderWithProviders'
import * as expenseApi from '@/features/expenses/api/expenseApi'
import { FinanceSearchPage } from './FinanceSearchPage'

function emptyPage(overrides: Partial<PagedExpenseResponse> = {}): PagedExpenseResponse {
  return { items: [], page: 1, pageSize: 20, totalRecords: 0, ...overrides }
}

describe('FinanceSearchPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('calls searchExpenses with an empty filter body on initial render', async () => {
    const searchSpy = vi.spyOn(expenseApi, 'searchExpenses').mockResolvedValue(emptyPage())

    renderWithProviders(<FinanceSearchPage />)

    await waitFor(() =>
      expect(searchSpy).toHaveBeenCalledWith(
        {
          expenseNumber: undefined,
          employeeName: undefined,
          category: undefined,
          status: undefined,
          fromDate: undefined,
          toDate: undefined,
          page: 1,
          pageSize: 20,
          sortBy: 'expenseDate',
          sortDirection: 'desc',
        },
        null,
      ),
    )
  })

  it('re-issues the search with an updated page size', async () => {
    const searchSpy = vi.spyOn(expenseApi, 'searchExpenses').mockResolvedValue(emptyPage())
    renderWithProviders(<FinanceSearchPage />)
    await screen.findByRole('combobox', { name: /page size/i }, { timeout: 3000 })

    fireEvent.click(screen.getByRole('combobox', { name: /page size/i }))
    const option50 = await screen.findByRole('option', { name: '50' })
    fireEvent.pointerDown(option50, { button: 0 })
    fireEvent.pointerUp(option50, { button: 0 })
    fireEvent.click(option50)

    await waitFor(() =>
      expect(searchSpy).toHaveBeenLastCalledWith(expect.objectContaining({ pageSize: 50 }), null),
    )
  })

  it('re-issues the search with an updated sortBy and sortDirection', async () => {
    const searchSpy = vi.spyOn(expenseApi, 'searchExpenses').mockResolvedValue(emptyPage())
    renderWithProviders(<FinanceSearchPage />)
    await screen.findByRole('combobox', { name: /sort by/i }, { timeout: 3000 })

    fireEvent.click(screen.getByRole('combobox', { name: /sort by/i }))
    const amountOption = await screen.findByRole('option', { name: /amount/i })
    fireEvent.pointerDown(amountOption, { button: 0 })
    fireEvent.pointerUp(amountOption, { button: 0 })
    fireEvent.click(amountOption)

    await waitFor(() =>
      expect(searchSpy).toHaveBeenLastCalledWith(
        expect.objectContaining({ sortBy: 'amount' }),
        null,
      ),
    )

    fireEvent.click(await screen.findByRole('combobox', { name: /sort direction/i }))
    const ascOption = await screen.findByRole('option', { name: /ascending/i })
    fireEvent.pointerDown(ascOption, { button: 0 })
    fireEvent.pointerUp(ascOption, { button: 0 })
    fireEvent.click(ascOption)

    await waitFor(() =>
      expect(searchSpy).toHaveBeenLastCalledWith(
        expect.objectContaining({ sortBy: 'amount', sortDirection: 'asc' }),
        null,
      ),
    )
  })

  it('offers only 20, 50, 100, and 500 as page-size options', async () => {
    vi.spyOn(expenseApi, 'searchExpenses').mockResolvedValue(emptyPage())
    renderWithProviders(<FinanceSearchPage />)
    await screen.findByRole('combobox', { name: /page size/i }, { timeout: 3000 })

    fireEvent.click(screen.getByRole('combobox', { name: /page size/i }))
    const options = await screen.findAllByRole('option')
    const optionLabels = options.map((option) => option.textContent)

    expect(optionLabels).toEqual(['20', '50', '100', '500'])
  })
})
