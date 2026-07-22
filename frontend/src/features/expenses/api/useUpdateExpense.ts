import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import type { ExpenseEnvelopeResponse } from '@/types/expense'
import { useAuthStore } from '@/store/authStore'
import { updateExpense, type ExpenseFormInput } from './expenseApi'

export function useUpdateExpense(id: string) {
  const accessToken = useAuthStore((state) => state.accessToken)
  const queryClient = useQueryClient()

  return useMutation<ExpenseEnvelopeResponse, ApiError, ExpenseFormInput>({
    mutationFn: (input) => updateExpense(id, input, accessToken),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['expenses'] })
    },
  })
}
