import { beforeEach, describe, expect, it, vi } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import * as authApi from '../api/authApi'
import { useAuthStore } from '@/store/authStore'
import {
  getStoredRefreshToken,
  performSilentRefresh,
  storeRefreshToken,
  useSessionBootstrap,
} from './useSessionBootstrap'
import type { User } from '@/types/auth'

const user: User = {
  id: 'user-1',
  email: 'a@b.com',
  employeeNumber: 'EMP001',
  firstName: 'Ada',
  lastName: 'Lovelace',
  role: 'Employee',
}

function base64url(value: object): string {
  return btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

// A far-future exp (default) keeps the proactive-refresh effect's timer
// from firing during unrelated tests — a non-JWT-shaped access token would
// make computeRefreshDelayMs return 0 and trigger an immediate extra
// refresh call, racing with what each test is actually asserting.
function makeAccessToken(secondsFromNow = 15 * 60): string {
  const exp = Date.now() / 1000 + secondsFromNow
  return `${base64url({ alg: 'HS256' })}.${base64url({ sub: 'user-1', exp })}.signature`
}

describe('performSilentRefresh', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('returns null without calling the API when no refresh token is stored', async () => {
    const refreshSpy = vi.spyOn(authApi, 'refresh')

    const result = await performSilentRefresh()

    expect(result).toBeNull()
    expect(refreshSpy).not.toHaveBeenCalled()
  })

  it('redeems the stored refresh token, fetches the user, and rotates the stored token', async () => {
    const accessToken = makeAccessToken()
    storeRefreshToken('old-refresh-token')
    vi.spyOn(authApi, 'refresh').mockResolvedValue({ accessToken, refreshToken: 'new-refresh-token' })
    vi.spyOn(authApi, 'me').mockResolvedValue(user)

    const result = await performSilentRefresh()

    expect(result).toEqual({ user, accessToken })
    expect(getStoredRefreshToken()).toBe('new-refresh-token')
  })

  it('clears the stored refresh token and returns null when refresh fails', async () => {
    storeRefreshToken('old-refresh-token')
    vi.spyOn(authApi, 'refresh').mockRejectedValue({ code: 'AUTHENTICATION_FAILED' })

    const result = await performSilentRefresh()

    expect(result).toBeNull()
    expect(getStoredRefreshToken()).toBeNull()
  })

  it('clears the stored refresh token and returns null when the /me call fails', async () => {
    storeRefreshToken('old-refresh-token')
    vi.spyOn(authApi, 'refresh').mockResolvedValue({
      accessToken: 'new-access-token',
      refreshToken: 'new-refresh-token',
    })
    vi.spyOn(authApi, 'me').mockRejectedValue({ code: 'AUTHENTICATION_FAILED' })

    const result = await performSilentRefresh()

    expect(result).toBeNull()
    expect(getStoredRefreshToken()).toBeNull()
  })
})

describe('useSessionBootstrap', () => {
  beforeEach(() => {
    localStorage.clear()
    useAuthStore.setState({ user: null, accessToken: null, status: 'bootstrapping' })
    vi.restoreAllMocks()
  })

  it('sets status to unauthenticated when no refresh token is stored', async () => {
    renderHook(() => useSessionBootstrap())

    await waitFor(() => {
      expect(useAuthStore.getState().status).toBe('unauthenticated')
    })
  })

  it('restores the session when a valid refresh token is stored', async () => {
    const accessToken = makeAccessToken()
    storeRefreshToken('old-refresh-token')
    vi.spyOn(authApi, 'refresh').mockResolvedValue({ accessToken, refreshToken: 'new-refresh-token' })
    vi.spyOn(authApi, 'me').mockResolvedValue(user)

    renderHook(() => useSessionBootstrap())

    await waitFor(() => {
      expect(useAuthStore.getState().status).toBe('authenticated')
    })
    expect(useAuthStore.getState().user).toEqual(user)
    expect(useAuthStore.getState().accessToken).toBe(accessToken)
  })

  it('clears the session when the stored refresh token is rejected', async () => {
    storeRefreshToken('stale-refresh-token')
    vi.spyOn(authApi, 'refresh').mockRejectedValue({ code: 'AUTHENTICATION_FAILED' })

    renderHook(() => useSessionBootstrap())

    await waitFor(() => {
      expect(useAuthStore.getState().status).toBe('unauthenticated')
    })
    expect(getStoredRefreshToken()).toBeNull()
  })

  it('never persists the access token to localStorage — only the refresh token is stored', async () => {
    const accessToken = makeAccessToken()
    storeRefreshToken('old-refresh-token')
    vi.spyOn(authApi, 'refresh').mockResolvedValue({ accessToken, refreshToken: 'new-refresh-token' })
    vi.spyOn(authApi, 'me').mockResolvedValue(user)

    renderHook(() => useSessionBootstrap())

    await waitFor(() => {
      expect(useAuthStore.getState().accessToken).toBe(accessToken)
    })

    for (let i = 0; i < localStorage.length; i++) {
      const key = localStorage.key(i)!
      expect(localStorage.getItem(key)).not.toBe(accessToken)
    }
  })

  it('proactively renews the access token before its 15-minute expiry', async () => {
    storeRefreshToken('current-refresh-token')
    const shortLivedToken = makeAccessToken(120) // expires in 2 min -> due after 1 min (60s margin)
    const renewedToken = makeAccessToken(900)
    const refreshMock = vi.spyOn(authApi, 'refresh')
    refreshMock.mockResolvedValueOnce({ accessToken: shortLivedToken, refreshToken: 'r1' })
    refreshMock.mockResolvedValueOnce({ accessToken: renewedToken, refreshToken: 'r2' })
    vi.spyOn(authApi, 'me').mockResolvedValue(user)

    vi.useFakeTimers()
    try {
      renderHook(() => useSessionBootstrap())

      await vi.advanceTimersByTimeAsync(0)
      expect(useAuthStore.getState().accessToken).toBe(shortLivedToken)

      await vi.advanceTimersByTimeAsync(60_000)

      expect(useAuthStore.getState().accessToken).toBe(renewedToken)
      expect(refreshMock).toHaveBeenCalledTimes(2)
    } finally {
      vi.useRealTimers()
    }
  })

  // The visibilitychange re-check (design.md D2/Risks: catches a proactive-refresh timer that
  // silently failed to fire, e.g. after a suspended tab) is intentionally not integration-tested
  // here — its two building blocks (computeRefreshDelayMs, performSilentRefresh) are each already
  // covered above/in scheduleProactiveRefresh.test.ts, and asserting the composed timer-vs-event
  // race deterministically would require fake-timer machinery disproportionate to what is a
  // defensive fallback, not a spec-required scenario. Covered by the manual smoke pass instead.
})
