import { useMutation } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import { resetPassword, type ResetPasswordInput } from './authApi'

export function useResetPassword() {
  return useMutation<void, ApiError, ResetPasswordInput>({
    mutationFn: (input: ResetPasswordInput) => resetPassword(input),
  })
}
