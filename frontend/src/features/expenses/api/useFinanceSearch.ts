import { useQuery } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import type { FinanceSearchParams, PagedExpenseResponse } from '@/types/expense'
import { useAuthStore } from '@/store/authStore'
import { searchExpenses } from './expenseApi'

export function useFinanceSearch(params: FinanceSearchParams) {
  const accessToken = useAuthStore((state) => state.accessToken)

  return useQuery<PagedExpenseResponse, ApiError>({
    queryKey: ['expenses', 'search', params],
    queryFn: () => searchExpenses(params, accessToken),
  })
}
