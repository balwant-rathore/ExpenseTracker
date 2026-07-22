import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import type { ExpenseEnvelopeResponse } from '@/types/expense'
import { useAuthStore } from '@/store/authStore'
import { reimburseExpense } from './expenseApi'

export function useReimburseExpense(id: string) {
  const accessToken = useAuthStore((state) => state.accessToken)
  const queryClient = useQueryClient()

  return useMutation<ExpenseEnvelopeResponse, ApiError, void>({
    mutationFn: () => reimburseExpense(id, accessToken),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['expenses'] })
    },
  })
}
