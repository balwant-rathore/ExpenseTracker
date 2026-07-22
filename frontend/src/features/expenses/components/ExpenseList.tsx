import { Link } from 'react-router-dom'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import type { ExpenseResponse, ExpenseSortField } from '@/types/expense'
import { categoryLabel, formatCurrency, statusLabel } from '../utils/expenseDisplay'

const PAGE_SIZES = [10, 20, 50, 100] as const

const SORT_FIELDS: { value: ExpenseSortField; label: string }[] = [
  { value: 'expenseDate', label: 'Expense date' },
  { value: 'expenseNumber', label: 'Expense number' },
  { value: 'createdAt', label: 'Created date' },
  { value: 'amount', label: 'Amount' },
  { value: 'submittedAt', label: 'Submitted date' },
  { value: 'approvedAt', label: 'Approved date' },
  { value: 'reimbursedAt', label: 'Reimbursed date' },
  { value: 'rejectedAt', label: 'Rejected date' },
]

interface ExpenseListProps {
  items: ExpenseResponse[]
  page: number
  pageSize: 10 | 20 | 50 | 100
  totalRecords: number
  sortBy: ExpenseSortField
  sortDirection: 'asc' | 'desc'
  /** The authenticated user's employeeNumber — see ExpenseDetail's prop doc for why. */
  currentUserEmployeeNumber?: string
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: 10 | 20 | 50 | 100) => void
  onSortByChange: (sortBy: ExpenseSortField) => void
  onSortDirectionChange: (sortDirection: 'asc' | 'desc') => void
}

export function ExpenseList({
  items,
  page,
  pageSize,
  totalRecords,
  sortBy,
  sortDirection,
  currentUserEmployeeNumber,
  onPageChange,
  onPageSizeChange,
  onSortByChange,
  onSortDirectionChange,
}: ExpenseListProps) {
  const totalPages = Math.max(1, Math.ceil(totalRecords / pageSize))

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-4">
        <div className="flex items-center gap-2">
          <label htmlFor="sort-by" className="text-sm text-muted-foreground">
            Sort by
          </label>
          <Select value={sortBy} onValueChange={(next) => onSortByChange(next as ExpenseSortField)}>
            <SelectTrigger id="sort-by">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {SORT_FIELDS.map((field) => (
                <SelectItem key={field.value} value={field.value}>
                  {field.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select
            value={sortDirection}
            onValueChange={(next) => onSortDirectionChange(next as 'asc' | 'desc')}
          >
            <SelectTrigger id="sort-direction">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="asc">Ascending</SelectItem>
              <SelectItem value="desc">Descending</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="flex items-center gap-2">
          <label htmlFor="page-size" className="text-sm text-muted-foreground">
            Page size
          </label>
          <Select
            value={String(pageSize)}
            onValueChange={(next) => onPageSizeChange(Number(next) as 10 | 20 | 50 | 100)}
          >
            <SelectTrigger id="page-size">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PAGE_SIZES.map((size) => (
                <SelectItem key={size} value={String(size)}>
                  {size}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Expense number</TableHead>
            <TableHead>Employee</TableHead>
            <TableHead>Category</TableHead>
            <TableHead>Amount</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Expense date</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.map((expense) => (
            <TableRow key={expense.id}>
              <TableCell>
                <Link to={`/expenses/${expense.id}`} className="underline underline-offset-4">
                  {expense.expenseNumber}
                </Link>
              </TableCell>
              <TableCell>
                <span className="flex items-center gap-2">
                  {expense.employeeName}
                  {currentUserEmployeeNumber && expense.employeeNumber === currentUserEmployeeNumber && (
                    <Badge variant="secondary">Mine</Badge>
                  )}
                </span>
              </TableCell>
              <TableCell>{categoryLabel(expense.category)}</TableCell>
              <TableCell>{formatCurrency(expense.amount, expense.currency)}</TableCell>
              <TableCell>{statusLabel(expense.status)}</TableCell>
              <TableCell>{expense.expenseDate}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <div className="flex items-center justify-between">
        <span className="text-sm text-muted-foreground">
          Page {page} of {totalPages} ({totalRecords} total)
        </span>
        <div className="flex gap-2">
          <Button
            variant="outline"
            disabled={page <= 1}
            onClick={() => onPageChange(page - 1)}
          >
            Previous
          </Button>
          <Button
            variant="outline"
            disabled={page >= totalPages}
            onClick={() => onPageChange(page + 1)}
          >
            Next
          </Button>
        </div>
      </div>
    </div>
  )
}
