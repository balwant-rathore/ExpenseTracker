import { useMutation } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import { forgotPassword } from './authApi'

export function useForgotPassword() {
  return useMutation<void, ApiError, string>({
    mutationFn: (email: string) => forgotPassword(email),
  })
}
