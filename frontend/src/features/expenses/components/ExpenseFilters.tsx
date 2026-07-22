import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { EXPENSE_CATEGORIES, EXPENSE_STATUSES, type ExpenseCategory, type ExpenseStatus } from '@/types/expense'
import { categoryLabel, statusLabel } from '../utils/expenseDisplay'

export interface ExpenseFiltersValue {
  status: ExpenseStatus | null
  category: ExpenseCategory | null
  fromDate: string
  toDate: string
}

interface ExpenseFiltersProps {
  value: ExpenseFiltersValue
  onChange: (value: ExpenseFiltersValue) => void
}

const CLEAR_VALUE = '__clear__'

export function ExpenseFilters({ value, onChange }: ExpenseFiltersProps) {
  return (
    <div className="flex flex-wrap items-end gap-4">
      <Field className="w-40">
        <FieldLabel htmlFor="filter-status">Status</FieldLabel>
        <Select
          value={value.status ?? CLEAR_VALUE}
          onValueChange={(next) =>
            onChange({ ...value, status: next === CLEAR_VALUE ? null : (next as ExpenseStatus) })
          }
        >
          <SelectTrigger id="filter-status">
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

      <Field className="w-48">
        <FieldLabel htmlFor="filter-category">Category (current page only)</FieldLabel>
        <Select
          value={value.category ?? CLEAR_VALUE}
          onValueChange={(next) =>
            onChange({ ...value, category: next === CLEAR_VALUE ? null : (next as ExpenseCategory) })
          }
        >
          <SelectTrigger id="filter-category">
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

      <Field className="w-40">
        <FieldLabel htmlFor="filter-from-date">From date (current page only)</FieldLabel>
        <Input
          id="filter-from-date"
          type="date"
          value={value.fromDate}
          onChange={(event) => onChange({ ...value, fromDate: event.target.value })}
        />
      </Field>

      <Field className="w-40">
        <FieldLabel htmlFor="filter-to-date">To date (current page only)</FieldLabel>
        <Input
          id="filter-to-date"
          type="date"
          value={value.toDate}
          onChange={(event) => onChange({ ...value, toDate: event.target.value })}
        />
      </Field>
    </div>
  )
}
