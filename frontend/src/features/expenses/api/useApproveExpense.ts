import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import type { ExpenseEnvelopeResponse } from '@/types/expense'
import { useAuthStore } from '@/store/authStore'
import { approveExpense } from './expenseApi'

export function useApproveExpense(id: string) {
  const accessToken = useAuthStore((state) => state.accessToken)
  const queryClient = useQueryClient()

  return useMutation<ExpenseEnvelopeResponse, ApiError, void>({
    mutationFn: () => approveExpense(id, accessToken),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['expenses'] })
    },
  })
}
