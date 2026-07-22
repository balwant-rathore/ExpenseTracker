import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import * as authApi from './authApi'
import { useAuthStore } from '@/store/authStore'
import { getStoredRefreshToken, storeRefreshToken } from '../session/useSessionBootstrap'
import type { User } from '@/types/auth'
import { useLogout } from './useLogout'

const user: User = {
  id: 'user-1',
  email: 'a@b.com',
  employeeNumber: 'EMP001',
  firstName: 'Ada',
  lastName: 'Lovelace',
  role: 'Employee',
}

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}

describe('useLogout', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    localStorage.clear()
    useAuthStore.setState({ user, accessToken: 'access-1', status: 'authenticated' })
  })

  it('calls the logout API with the stored refresh and current access token, then clears local session state', async () => {
    storeRefreshToken('refresh-1')
    const logoutSpy = vi.spyOn(authApi, 'logout').mockResolvedValue(undefined)

    const { result } = renderHook(() => useLogout(), { wrapper })
    result.current.mutate()

    await waitFor(() => expect(useAuthStore.getState().status).toBe('unauthenticated'))
    expect(logoutSpy).toHaveBeenCalledWith('refresh-1', 'access-1')
    expect(useAuthStore.getState().user).toBeNull()
    expect(useAuthStore.getState().accessToken).toBeNull()
    expect(getStoredRefreshToken()).toBeNull()
  })

  it('clears local session state even when the logout API call fails', async () => {
    storeRefreshToken('refresh-1')
    vi.spyOn(authApi, 'logout').mockRejectedValue({ code: 'INTERNAL_SERVER_ERROR' })

    const { result } = renderHook(() => useLogout(), { wrapper })
    result.current.mutate()

    await waitFor(() => expect(useAuthStore.getState().status).toBe('unauthenticated'))
    expect(useAuthStore.getState().user).toBeNull()
    expect(useAuthStore.getState().accessToken).toBeNull()
    expect(getStoredRefreshToken()).toBeNull()
  })
})
