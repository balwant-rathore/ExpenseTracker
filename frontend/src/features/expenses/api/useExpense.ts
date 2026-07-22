import { useQuery } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import type { ExpenseEnvelopeResponse } from '@/types/expense'
import { useAuthStore } from '@/store/authStore'
import { getExpense } from './expenseApi'

export function useExpense(id: string) {
  const accessToken = useAuthStore((state) => state.accessToken)

  return useQuery<ExpenseEnvelopeResponse, ApiError>({
    queryKey: ['expenses', 'detail', id],
    queryFn: () => getExpense(id, accessToken),
  })
}
