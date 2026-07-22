import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import type { ExpenseEnvelopeResponse } from '@/types/expense'
import { useAuthStore } from '@/store/authStore'
import { rejectExpense } from './expenseApi'

export function useRejectExpense(id: string) {
  const accessToken = useAuthStore((state) => state.accessToken)
  const queryClient = useQueryClient()

  return useMutation<ExpenseEnvelopeResponse, ApiError, string>({
    mutationFn: (rejectionComment) => rejectExpense(id, rejectionComment, accessToken),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['expenses'] })
    },
  })
}
