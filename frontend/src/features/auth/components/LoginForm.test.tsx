import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import * as authApi from '../api/authApi'
import { useAuthStore } from '@/store/authStore'
import { renderWithProviders } from '@/test/renderWithProviders'
import type { User } from '@/types/auth'
import { LoginForm } from './LoginForm'

const user: User = {
  id: 'user-1',
  email: 'a@b.com',
  employeeNumber: 'EMP001',
  firstName: 'Ada',
  lastName: 'Lovelace',
  role: 'Employee',
}

describe('LoginForm', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    localStorage.clear()
    useAuthStore.setState({ user: null, accessToken: null, status: 'unauthenticated' })
  })

  it('calls login with exactly email and password on a valid submit', async () => {
    const loginSpy = vi
      .spyOn(authApi, 'login')
      .mockResolvedValue({ user, accessToken: 'a', refreshToken: 'r' })

    renderWithProviders(<LoginForm />)

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password1' } })
    fireEvent.click(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() =>
      expect(loginSpy).toHaveBeenCalledWith({ email: 'a@b.com', password: 'password1' }),
    )
  })

  it('blocks submission and makes no API call when fields are empty', async () => {
    const loginSpy = vi.spyOn(authApi, 'login')

    renderWithProviders(<LoginForm />)
    fireEvent.click(screen.getByRole('button', { name: /log in/i }))

    expect(await screen.findByText('Password is required.')).toBeInTheDocument()
    expect(loginSpy).not.toHaveBeenCalled()
  })

  it('shows one generic message for invalid credentials, not attached to a specific field', async () => {
    vi.spyOn(authApi, 'login').mockRejectedValue({
      code: 'AUTHENTICATION_FAILED',
      message: 'Invalid email or password.',
      fields: [],
      traceId: 't',
    })

    renderWithProviders(<LoginForm />)
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'wrongpass1' } })
    fireEvent.click(screen.getByRole('button', { name: /log in/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Invalid email or password.')
  })

  it('shows a generic throttling message on a 429 response', async () => {
    vi.spyOn(authApi, 'login').mockRejectedValue({
      code: 'RATE_LIMIT_EXCEEDED',
      message: 'Too many requests.',
      fields: [],
      traceId: 't',
    })

    renderWithProviders(<LoginForm />)
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password1' } })
    fireEvent.click(screen.getByRole('button', { name: /log in/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Too many attempts')
  })

  it('stores the session on success, regardless of role', async () => {
    vi.spyOn(authApi, 'login').mockResolvedValue({
      user,
      accessToken: 'new-access',
      refreshToken: 'new-refresh',
    })

    renderWithProviders(<LoginForm />)
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password1' } })
    fireEvent.click(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() => expect(useAuthStore.getState().status).toBe('authenticated'))
    expect(useAuthStore.getState().user).toEqual(user)
    expect(useAuthStore.getState().accessToken).toBe('new-access')
  })

  it('navigates to /dashboard on success (real route assertion, not just store state)', async () => {
    vi.spyOn(authApi, 'login').mockResolvedValue({
      user,
      accessToken: 'new-access',
      refreshToken: 'new-refresh',
    })
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/login']}>
          <Routes>
            <Route path="/login" element={<LoginForm />} />
            <Route path="/dashboard" element={<div>dashboard stub</div>} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    )

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password1' } })
    fireEvent.click(screen.getByRole('button', { name: /log in/i }))

    expect(await screen.findByText('dashboard stub')).toBeInTheDocument()
  })
})
