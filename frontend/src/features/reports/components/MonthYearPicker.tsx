import { Field, FieldLabel } from '@/components/ui/field'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

const MONTH_NAMES = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
]

interface MonthYearPickerProps {
  year: number
  month: number
  years: number[]
  onYearChange: (year: number) => void
  onMonthChange: (month: number) => void
}

export function MonthYearPicker({ year, month, years, onYearChange, onMonthChange }: MonthYearPickerProps) {
  return (
    <div className="flex flex-wrap items-end gap-4">
      <Field className="w-40">
        <FieldLabel htmlFor="report-month">Month</FieldLabel>
        <Select value={String(month)} onValueChange={(next) => onMonthChange(Number(next))}>
          <SelectTrigger id="report-month">
            <SelectValue>{(value: string) => MONTH_NAMES[Number(value) - 1]}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            {MONTH_NAMES.map((name, index) => (
              <SelectItem key={name} value={String(index + 1)}>
                {name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>

      <Field className="w-32">
        <FieldLabel htmlFor="report-year">Year</FieldLabel>
        <Select value={String(year)} onValueChange={(next) => onYearChange(Number(next))}>
          <SelectTrigger id="report-year">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {years.map((y) => (
              <SelectItem key={y} value={String(y)}>
                {y}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>
    </div>
  )
}
