import { describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '@/test/renderWithProviders'
import { FinanceSearchFilters } from './FinanceSearchFilters'

describe('FinanceSearchFilters', () => {
  it('submits expenseNumber alone when only that field is filled', () => {
    const onSearch = vi.fn()
    renderWithProviders(<FinanceSearchFilters onSearch={onSearch} />)

    fireEvent.change(screen.getByLabelText(/expense number/i), { target: { value: 'EXP-1' } })
    fireEvent.click(screen.getByRole('button', { name: /^search$/i }))

    expect(onSearch).toHaveBeenCalledWith({
      expenseNumber: 'EXP-1',
      employeeName: undefined,
      category: undefined,
      status: undefined,
      fromDate: undefined,
      toDate: undefined,
    })
  })

  it('submits employeeName alone when only that field is filled', () => {
    const onSearch = vi.fn()
    renderWithProviders(<FinanceSearchFilters onSearch={onSearch} />)

    fireEvent.change(screen.getByLabelText(/employee name/i), { target: { value: 'Ada' } })
    fireEvent.click(screen.getByRole('button', { name: /^search$/i }))

    expect(onSearch).toHaveBeenCalledWith(expect.objectContaining({ employeeName: 'Ada' }))
  })

  it('submits a from/to date range', () => {
    const onSearch = vi.fn()
    renderWithProviders(<FinanceSearchFilters onSearch={onSearch} />)

    fireEvent.change(screen.getByLabelText(/from date/i), { target: { value: '2026-01-01' } })
    fireEvent.change(screen.getByLabelText(/to date/i), { target: { value: '2026-01-31' } })
    fireEvent.click(screen.getByRole('button', { name: /^search$/i }))

    expect(onSearch).toHaveBeenCalledWith(
      expect.objectContaining({ fromDate: '2026-01-01', toDate: '2026-01-31' }),
    )
  })

  it('combines category and status into one submitted request', async () => {
    const onSearch = vi.fn()
    renderWithProviders(<FinanceSearchFilters onSearch={onSearch} />)

    fireEvent.click(screen.getByRole('combobox', { name: /category/i }))
    const travelOption = await screen.findByRole('option', { name: /travel/i })
    fireEvent.pointerDown(travelOption, { button: 0 })
    fireEvent.pointerUp(travelOption, { button: 0 })
    fireEvent.click(travelOption)
    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: /category/i })).toHaveTextContent(/travel/i),
    )

    fireEvent.click(screen.getByRole('combobox', { name: /status/i }))
    const submittedOption = await screen.findByRole('option', { name: /submitted/i })
    fireEvent.pointerDown(submittedOption, { button: 0 })
    fireEvent.pointerUp(submittedOption, { button: 0 })
    fireEvent.click(submittedOption)
    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: /status/i })).toHaveTextContent(/submitted/i),
    )

    fireEvent.click(screen.getByRole('button', { name: /^search$/i }))

    expect(onSearch).toHaveBeenCalledWith(
      expect.objectContaining({ category: 'Travel', status: 'Submitted' }),
    )
  })

  it('clearing all filters re-issues an empty params object', () => {
    const onSearch = vi.fn()
    renderWithProviders(<FinanceSearchFilters onSearch={onSearch} />)

    fireEvent.change(screen.getByLabelText(/expense number/i), { target: { value: 'EXP-1' } })
    fireEvent.click(screen.getByRole('button', { name: /clear filters/i }))

    expect(onSearch).toHaveBeenCalledWith({
      expenseNumber: undefined,
      employeeName: undefined,
      category: undefined,
      status: undefined,
      fromDate: undefined,
      toDate: undefined,
    })
  })
})
