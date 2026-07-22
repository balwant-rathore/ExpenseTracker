import { useQuery } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import type { PagedExpenseResponse } from '@/types/expense'
import { useAuthStore } from '@/store/authStore'
import { listExpenses, type ExpenseListParams } from './expenseApi'

export function useExpenseList(params: ExpenseListParams) {
  const accessToken = useAuthStore((state) => state.accessToken)

  return useQuery<PagedExpenseResponse, ApiError>({
    queryKey: ['expenses', 'list', params],
    queryFn: () => listExpenses(params, accessToken),
  })
}
