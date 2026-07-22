import { useEffect } from 'react'
import { useAuthStore } from '@/store/authStore'
import type { User } from '@/types/auth'
import * as authApi from '../api/authApi'
import { computeRefreshDelayMs, scheduleProactiveRefresh } from './scheduleProactiveRefresh'

const REFRESH_TOKEN_STORAGE_KEY = 'expensetracker.refreshToken'

export function getStoredRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_STORAGE_KEY)
}

export function storeRefreshToken(token: string): void {
  localStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, token)
}

export function clearStoredRefreshToken(): void {
  localStorage.removeItem(REFRESH_TOKEN_STORAGE_KEY)
}

interface RefreshedSession {
  user: User
  accessToken: string
}

let inFlightRefresh: Promise<RefreshedSession | null> | null = null

/**
 * Redeems the stored refresh token for a new token pair, then fetches the
 * current user (POST /api/auth/refresh returns tokens only — see
 * docs/decisions/ADR-0017-frontend-token-storage.md). Rotates the stored
 * refresh token on success. Returns null and clears the stored refresh
 * token on any failure — callers don't need their own try/catch.
 *
 * Coalesces concurrent callers onto a single in-flight request. Refresh
 * tokens are single-use and rotate on redemption (docs/SDS.md §4.4); two
 * genuinely simultaneous calls (e.g. React StrictMode's dev-mode double
 * effect invocation on mount) would otherwise both redeem the same stored
 * token, and the loser's request hits the backend's reuse-of-a-revoked-
 * token safeguard, which revokes every refresh token for that user —
 * silently killing the session the winner just established.
 */
export async function performSilentRefresh(): Promise<RefreshedSession | null> {
  if (inFlightRefresh) {
    return inFlightRefresh
  }

  const storedRefreshToken = getStoredRefreshToken()
  if (!storedRefreshToken) {
    return null
  }

  inFlightRefresh = (async () => {
    try {
      const refreshResult = await authApi.refresh(storedRefreshToken)
      const user = await authApi.me(refreshResult.accessToken)
      storeRefreshToken(refreshResult.refreshToken)
      return { user, accessToken: refreshResult.accessToken }
    } catch {
      clearStoredRefreshToken()
      return null
    } finally {
      inFlightRefresh = null
    }
  })()

  return inFlightRefresh
}

/**
 * Mounted once above the router. Silently restores the session from a
 * stored refresh token on load, then keeps the access token proactively
 * renewed for the lifetime of the app — see design.md D2/D2a.
 */
export function useSessionBootstrap(): void {
  const status = useAuthStore((state) => state.status)
  const accessToken = useAuthStore((state) => state.accessToken)
  const setSession = useAuthStore((state) => state.setSession)
  const setAccessToken = useAuthStore((state) => state.setAccessToken)
  const clearSession = useAuthStore((state) => state.clearSession)

  useEffect(() => {
    let cancelled = false

    void (async () => {
      const restored = await performSilentRefresh()
      if (cancelled) {
        return
      }
      if (restored) {
        setSession(restored.user, restored.accessToken)
      } else {
        clearSession()
      }
    })()

    return () => {
      cancelled = true
    }
    // Runs exactly once on mount — session store setters are stable.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (status !== 'authenticated' || !accessToken) {
      return
    }

    const cancel = scheduleProactiveRefresh(accessToken, () => {
      void (async () => {
        const restored = await performSilentRefresh()
        if (restored) {
          setAccessToken(restored.accessToken)
        } else {
          clearSession()
        }
      })()
    })

    return cancel
  }, [status, accessToken, setAccessToken, clearSession])

  useEffect(() => {
    function handleVisibilityChange() {
      if (document.visibilityState !== 'visible') {
        return
      }
      const state = useAuthStore.getState()
      if (state.status !== 'authenticated' || !state.accessToken) {
        return
      }
      if (computeRefreshDelayMs(state.accessToken) > 0) {
        return
      }
      void (async () => {
        const restored = await performSilentRefresh()
        if (restored) {
          setAccessToken(restored.accessToken)
        } else {
          clearSession()
        }
      })()
    }

    document.addEventListener('visibilitychange', handleVisibilityChange)
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange)
  }, [setAccessToken, clearSession])
}
