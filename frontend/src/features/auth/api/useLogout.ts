import { useMutation } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import { useAuthStore } from '@/store/authStore'
import { clearStoredRefreshToken, getStoredRefreshToken } from '../session/useSessionBootstrap'
import { logout } from './authApi'

/**
 * Clears the local session unconditionally (onSettled, not onSuccess) —
 * per the frontend-session-management spec, logout must not leave the user
 * "stuck" logged in just because the API call itself failed or timed out.
 */
export function useLogout() {
  const accessToken = useAuthStore((state) => state.accessToken)
  const clearSession = useAuthStore((state) => state.clearSession)

  return useMutation<void, ApiError, void>({
    mutationFn: () => {
      const refreshToken = getStoredRefreshToken()
      if (!refreshToken) {
        return Promise.resolve()
      }
      return logout(refreshToken, accessToken)
    },
    onSettled: () => {
      clearStoredRefreshToken()
      clearSession()
    },
  })
}
