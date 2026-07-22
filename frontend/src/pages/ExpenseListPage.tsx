import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/store/authStore'
import { useExpenseList } from '@/features/expenses/api/useExpenseList'
import { ExpenseFilters, type ExpenseFiltersValue } from '@/features/expenses/components/ExpenseFilters'
import { ExpenseList } from '@/features/expenses/components/ExpenseList'
import type { ExpenseSortField } from '@/types/expense'

const EMPTY_FILTERS: ExpenseFiltersValue = {
  status: null,
  category: null,
  fromDate: '',
  toDate: '',
}

export function ExpenseListPage() {
  const user = useAuthStore((state) => state.user)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState<10 | 20 | 50 | 100>(20)
  const [sortBy, setSortBy] = useState<ExpenseSortField>('expenseDate')
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('desc')
  const [filters, setFilters] = useState<ExpenseFiltersValue>(EMPTY_FILTERS)

  const { data, isLoading } = useExpenseList({
    page,
    pageSize,
    sortBy,
    sortDirection,
    status: filters.status ?? undefined,
  })

  const filteredItems = useMemo(() => {
    const items = data?.items ?? []
    return items.filter((expense) => {
      if (filters.category && expense.category !== filters.category) {
        return false
      }
      if (filters.fromDate && expense.expenseDate < filters.fromDate) {
        return false
      }
      if (filters.toDate && expense.expenseDate > filters.toDate) {
        return false
      }
      return true
    })
  }, [data, filters.category, filters.fromDate, filters.toDate])

  return (
    <div className="flex flex-col gap-4 p-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-medium">Expenses</h1>
        <Button render={<Link to="/expenses/new" />}>New expense</Button>
      </div>

      <ExpenseFilters
        value={filters}
        onChange={(next) => {
          setFilters(next)
          setPage(1)
        }}
      />

      {isLoading ? (
        <div>Loading…</div>
      ) : (
        <ExpenseList
          items={filteredItems}
          page={data?.page ?? page}
          pageSize={pageSize}
          totalRecords={data?.totalRecords ?? 0}
          sortBy={sortBy}
          sortDirection={sortDirection}
          currentUserEmployeeNumber={user?.employeeNumber}
          onPageChange={setPage}
          onPageSizeChange={(next) => {
            setPageSize(next)
            setPage(1)
          }}
          onSortByChange={(next) => {
            setSortBy(next)
            setPage(1)
          }}
          onSortDirectionChange={(next) => {
            setSortDirection(next)
            setPage(1)
          }}
        />
      )}
    </div>
  )
}
