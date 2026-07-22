import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from './authStore'
import type { User } from '@/types/auth'

const user: User = {
  id: 'user-1',
  email: 'a@b.com',
  employeeNumber: 'EMP001',
  firstName: 'Ada',
  lastName: 'Lovelace',
  role: 'Employee',
}

describe('useAuthStore', () => {
  beforeEach(() => {
    useAuthStore.setState({ user: null, accessToken: null, status: 'bootstrapping' })
  })

  it('starts in the bootstrapping status with no user or token', () => {
    const state = useAuthStore.getState()
    expect(state.status).toBe('bootstrapping')
    expect(state.user).toBeNull()
    expect(state.accessToken).toBeNull()
  })

  it('setSession stores the user and access token and marks authenticated', () => {
    useAuthStore.getState().setSession(user, 'token-1')

    const state = useAuthStore.getState()
    expect(state.user).toEqual(user)
    expect(state.accessToken).toBe('token-1')
    expect(state.status).toBe('authenticated')
  })

  it('setAccessToken replaces only the access token, leaving user/status untouched', () => {
    useAuthStore.getState().setSession(user, 'token-1')
    useAuthStore.getState().setAccessToken('token-2')

    const state = useAuthStore.getState()
    expect(state.accessToken).toBe('token-2')
    expect(state.user).toEqual(user)
    expect(state.status).toBe('authenticated')
  })

  it('clearSession resets user, token, and status to unauthenticated', () => {
    useAuthStore.getState().setSession(user, 'token-1')
    useAuthStore.getState().clearSession()

    const state = useAuthStore.getState()
    expect(state.user).toBeNull()
    expect(state.accessToken).toBeNull()
    expect(state.status).toBe('unauthenticated')
  })
})
