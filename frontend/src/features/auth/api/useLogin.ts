import { useMutation } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import { useAuthStore } from '@/store/authStore'
import { storeRefreshToken } from '../session/useSessionBootstrap'
import { login, type AuthResponse, type LoginInput } from './authApi'

export function useLogin() {
  const setSession = useAuthStore((state) => state.setSession)

  return useMutation<AuthResponse, ApiError, LoginInput>({
    mutationFn: (input: LoginInput) => login(input),
    onSuccess: (data) => {
      storeRefreshToken(data.refreshToken)
      setSession(data.user, data.accessToken)
    },
  })
}
