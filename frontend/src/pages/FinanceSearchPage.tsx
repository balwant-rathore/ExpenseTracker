import { useState } from 'react'
import { useFinanceSearch } from '@/features/expenses/api/useFinanceSearch'
import {
  FinanceSearchFilters,
  type FinanceSearchFilterValues,
} from '@/features/expenses/components/FinanceSearchFilters'
import { FinanceSearchResults } from '@/features/expenses/components/FinanceSearchResults'
import type { ExpenseSortField } from '@/types/expense'

const EMPTY_FILTERS: FinanceSearchFilterValues = {
  expenseNumber: undefined,
  employeeName: undefined,
  category: undefined,
  status: undefined,
  fromDate: undefined,
  toDate: undefined,
}

export function FinanceSearchPage() {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState<20 | 50 | 100 | 500>(20)
  const [sortBy, setSortBy] = useState<ExpenseSortField>('expenseDate')
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('desc')
  const [filters, setFilters] = useState<FinanceSearchFilterValues>(EMPTY_FILTERS)

  const { data, isLoading } = useFinanceSearch({
    ...filters,
    page,
    pageSize,
    sortBy,
    sortDirection,
  })

  return (
    <div className="flex flex-col gap-4 p-4">
      <h1 className="text-xl font-medium">Finance Search</h1>

      <FinanceSearchFilters
        onSearch={(next) => {
          setFilters(next)
          setPage(1)
        }}
      />

      {isLoading ? (
        <div>Loading…</div>
      ) : (
        <FinanceSearchResults
          items={data?.items ?? []}
          page={data?.page ?? page}
          pageSize={pageSize}
          totalRecords={data?.totalRecords ?? 0}
          sortBy={sortBy}
          sortDirection={sortDirection}
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
