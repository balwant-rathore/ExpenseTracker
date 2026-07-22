import { useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import {
  EXPENSE_CATEGORIES,
  EXPENSE_STATUSES,
  type ExpenseCategory,
  type ExpenseStatus,
  type FinanceSearchParams,
} from '@/types/expense'
import { categoryLabel, statusLabel } from '../utils/expenseDisplay'

const CLEAR_VALUE = '__clear__'

export type FinanceSearchFilterValues = Pick<
  FinanceSearchParams,
  'expenseNumber' | 'employeeName' | 'category' | 'status' | 'fromDate' | 'toDate'
>

interface FinanceSearchFilterState {
  expenseNumber: string
  employeeName: string
  category: ExpenseCategory | null
  status: ExpenseStatus | null
  fromDate: string
  toDate: string
}

const EMPTY_STATE: FinanceSearchFilterState = {
  expenseNumber: '',
  employeeName: '',
  category: null,
  status: null,
  fromDate: '',
  toDate: '',
}

function toFilterValues(state: FinanceSearchFilterState): FinanceSearchFilterValues {
  return {
    expenseNumber: state.expenseNumber.trim() || undefined,
    employeeName: state.employeeName.trim() || undefined,
    category: state.category ?? undefined,
    status: state.status ?? undefined,
    fromDate: state.fromDate || undefined,
    toDate: state.toDate || undefined,
  }
}

interface FinanceSearchFiltersProps {
  onSearch: (filters: FinanceSearchFilterValues) => void
}

export function FinanceSearchFilters({ onSearch }: FinanceSearchFiltersProps) {
  const [state, setState] = useState<FinanceSearchFilterState>(EMPTY_STATE)

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    onSearch(toFilterValues(state))
  }

  function handleClear() {
    setState(EMPTY_STATE)
    onSearch(toFilterValues(EMPTY_STATE))
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-wrap items-end gap-4">
      <Field className="w-48">
        <FieldLabel htmlFor="search-expense-number">Expense number</FieldLabel>
        <Input
          id="search-expense-number"
          value={state.expenseNumber}
          onChange={(event) => setState((prev) => ({ ...prev, expenseNumber: event.target.value }))}
        />
      </Field>

      <Field className="w-48">
        <FieldLabel htmlFor="search-employee-name">Employee name</FieldLabel>
        <Input
          id="search-employee-name"
          value={state.employeeName}
          onChange={(event) => setState((prev) => ({ ...prev, employeeName: event.target.value }))}
        />
      </Field>

      <Field className="w-48">
        <FieldLabel htmlFor="search-category">Category</FieldLabel>
        <Select
          value={state.category ?? CLEAR_VALUE}
          onValueChange={(next) =>
            setState((prev) => ({
              ...prev,
              category: next === CLEAR_VALUE ? null : (next as ExpenseCategory),
            }))
          }
        >
          <SelectTrigger id="search-category">
            <SelectValue placeholder="All categories" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={CLEAR_VALUE}>All categories</SelectItem>
            {EXPENSE_CATEGORIES.map((category) => (
              <SelectItem key={category} value={category}>
                {categoryLabel(category)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>

      <Field className="w-48">
        <FieldLabel htmlFor="search-status">Status</FieldLabel>
        <Select
          value={state.status ?? CLEAR_VALUE}
          onValueChange={(next) =>
            setState((prev) => ({
              ...prev,
              status: next === CLEAR_VALUE ? null : (next as ExpenseStatus),
            }))
          }
        >
          <SelectTrigger id="search-status">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={CLEAR_VALUE}>All statuses</SelectItem>
            {EXPENSE_STATUSES.map((status) => (
              <SelectItem key={status} value={status}>
                {statusLabel(status)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>

      <Field className="w-40">
        <FieldLabel htmlFor="search-from-date">From date</FieldLabel>
        <Input
          id="search-from-date"
          type="date"
          value={state.fromDate}
          onChange={(event) => setState((prev) => ({ ...prev, fromDate: event.target.value }))}
        />
      </Field>

      <Field className="w-40">
        <FieldLabel htmlFor="search-to-date">To date</FieldLabel>
        <Input
          id="search-to-date"
          type="date"
          value={state.toDate}
          onChange={(event) => setState((prev) => ({ ...prev, toDate: event.target.value }))}
        />
      </Field>

      <div className="flex gap-2">
        <Button type="submit">Search</Button>
        <Button type="button" variant="outline" onClick={handleClear}>
          Clear filters
        </Button>
      </div>
    </form>
  )
}
