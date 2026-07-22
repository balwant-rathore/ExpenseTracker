import { useMutation } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import { useAuthStore } from '@/store/authStore'
import { storeRefreshToken } from '../session/useSessionBootstrap'
import { register, type AuthResponse, type RegisterInput } from './authApi'

export function useRegister() {
  const setSession = useAuthStore((state) => state.setSession)

  return useMutation<AuthResponse, ApiError, RegisterInput>({
    mutationFn: (input: RegisterInput) => register(input),
    onSuccess: (data) => {
      storeRefreshToken(data.refreshToken)
      setSession(data.user, data.accessToken)
    },
  })
}
