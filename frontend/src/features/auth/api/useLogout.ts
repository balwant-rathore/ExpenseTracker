import { useMutation } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import { useAuthStore } from '@/store/authStore'
import { useBreadcrumbStore } from '@/store/breadcrumbStore'
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
      // A subsequent login in the same SPA session must not inherit the previous user's
      // breadcrumb trail (design.md D6) — a hard reload already clears it implicitly, but
      // logout→login without a reload would not.
      useBreadcrumbStore.getState().reset()
    },
  })
}
