import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import type { ExpenseEnvelopeResponse } from '@/types/expense'
import { useAuthStore } from '@/store/authStore'
import { complianceApproveExpense } from './expenseApi'

export function useComplianceApprove(id: string) {
  const accessToken = useAuthStore((state) => state.accessToken)
  const queryClient = useQueryClient()

  return useMutation<ExpenseEnvelopeResponse, ApiError, void>({
    mutationFn: () => complianceApproveExpense(id, accessToken),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['expenses'] })
    },
  })
}
