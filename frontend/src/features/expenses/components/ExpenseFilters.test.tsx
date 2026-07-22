import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { selectOption } from '@/test/selectOption'
import { ExpenseFilters, type ExpenseFiltersValue } from './ExpenseFilters'

const emptyValue: ExpenseFiltersValue = { status: null, category: null, fromDate: '', toDate: '' }

describe('ExpenseFilters', () => {
  it('selecting a status calls onChange with that status', async () => {
    const onChange = vi.fn()
    render(<ExpenseFilters value={emptyValue} onChange={onChange} />)

    await selectOption('Status', 'Submitted')

    expect(onChange).toHaveBeenCalledWith({ ...emptyValue, status: 'Submitted' })
  })

  it('clearing the status filter restores null (no status)', async () => {
    const onChange = vi.fn()
    render(<ExpenseFilters value={{ ...emptyValue, status: 'Submitted' }} onChange={onChange} />)

    await selectOption('Status', 'All statuses')

    expect(onChange).toHaveBeenCalledWith({ ...emptyValue, status: null })
  })

  it('selecting a category calls onChange with that category', async () => {
    const onChange = vi.fn()
    render(<ExpenseFilters value={emptyValue} onChange={onChange} />)

    await selectOption('Category (current page only)', 'Travel')

    expect(onChange).toHaveBeenCalledWith({ ...emptyValue, category: 'Travel' })
  })

  it('changing the from/to date calls onChange with the new date range', () => {
    const onChange = vi.fn()
    render(<ExpenseFilters value={emptyValue} onChange={onChange} />)

    fireEvent.change(screen.getByLabelText('From date (current page only)'), {
      target: { value: '2026-01-01' },
    })
    expect(onChange).toHaveBeenCalledWith({ ...emptyValue, fromDate: '2026-01-01' })

    fireEvent.change(screen.getByLabelText('To date (current page only)'), {
      target: { value: '2026-01-31' },
    })
    expect(onChange).toHaveBeenCalledWith({ ...emptyValue, toDate: '2026-01-31' })
  })
})
