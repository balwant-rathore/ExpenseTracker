import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import * as authApi from '../api/authApi'
import { useAuthStore } from '@/store/authStore'
import { renderWithProviders } from '@/test/renderWithProviders'
import type { User } from '@/types/auth'
import { RegistrationForm } from './RegistrationForm'

const user: User = {
  id: 'user-1',
  email: 'a@b.com',
  employeeNumber: 'EMP001',
  firstName: 'Ada',
  lastName: 'Lovelace',
  role: 'Employee',
}

function fillValidForm() {
  fireEvent.change(screen.getByLabelText('Employee number'), { target: { value: 'EMP001' } })
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password1' } })
  fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'password1' } })
}

describe('RegistrationForm', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    localStorage.clear()
    useAuthStore.setState({ user: null, accessToken: null, status: 'unauthenticated' })
  })

  it('calls register with exactly employeeNumber, email, and password — never confirmPassword', async () => {
    const registerSpy = vi
      .spyOn(authApi, 'register')
      .mockResolvedValue({ user, accessToken: 'a', refreshToken: 'r' })

    renderWithProviders(<RegistrationForm />)
    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /create account/i }))

    await waitFor(() =>
      expect(registerSpy).toHaveBeenCalledWith({
        employeeNumber: 'EMP001',
        email: 'a@b.com',
        password: 'password1',
      }),
    )
  })

  it('blocks submission when confirmPassword does not match password', async () => {
    const registerSpy = vi.spyOn(authApi, 'register')

    renderWithProviders(<RegistrationForm />)
    fireEvent.change(screen.getByLabelText('Employee number'), { target: { value: 'EMP001' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password1' } })
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'different1' } })
    fireEvent.click(screen.getByRole('button', { name: /create account/i }))

    expect(await screen.findByText('Passwords do not match.')).toBeInTheDocument()
    expect(registerSpy).not.toHaveBeenCalled()
  })

  it('renders a 400 field-level error next to the offending field', async () => {
    vi.spyOn(authApi, 'register').mockRejectedValue({
      code: 'VALIDATION_ERROR',
      message: 'Password does not meet complexity requirements.',
      fields: ['password'],
      traceId: 't',
    })

    renderWithProviders(<RegistrationForm />)
    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /create account/i }))

    expect(
      await screen.findByText('Password does not meet complexity requirements.'),
    ).toBeInTheDocument()
  })

  it('renders a 400 with multiple simultaneously-invalid fields, matching the backend\'s actual PascalCase FluentValidation property names', async () => {
    // AuthController's generic FluentValidation path returns e.Errors.Select(e => e.PropertyName)
    // verbatim (PascalCase, e.g. "Email"/"Password"), unlike the lowerCamelCase hardcoded by a
    // couple of service-level checks — this exercises hasFieldError's case-insensitive matching
    // against that real shape for more than one field at once (gap caught by /review).
    vi.spyOn(authApi, 'register').mockRejectedValue({
      code: 'VALIDATION_ERROR',
      message: 'One or more fields are invalid.',
      fields: ['Email', 'Password'],
      traceId: 't',
    })

    renderWithProviders(<RegistrationForm />)
    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /create account/i }))

    const messages = await screen.findAllByText('One or more fields are invalid.')
    expect(messages).toHaveLength(2)
  })

  it('shows a generic throttling message on a 429 response', async () => {
    // FRS §3.5.2: registration is rate-limited the same as the other three auth endpoints —
    // gap caught by /review (the original spec delta never listed this scenario, even though the
    // identical gap had already been caught and fixed for reset-password in a prior pass).
    vi.spyOn(authApi, 'register').mockRejectedValue({
      code: 'RATE_LIMIT_EXCEEDED',
      message: 'Too many requests.',
      fields: [],
      traceId: 't',
    })

    renderWithProviders(<RegistrationForm />)
    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /create account/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Too many attempts')
  })

  it('renders the generic duplicate-email message on a 409 conflict', async () => {
    vi.spyOn(authApi, 'register').mockRejectedValue({
      code: 'RESOURCE_CONFLICT',
      message: 'This email is already registered.',
      fields: [],
      traceId: 't',
    })

    renderWithProviders(<RegistrationForm />)
    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /create account/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('This email is already registered.')
  })

  it('renders the generic 422 message against the employeeNumber field', async () => {
    vi.spyOn(authApi, 'register').mockRejectedValue({
      code: 'BUSINESS_RULE_VIOLATION',
      message: 'Registration could not be completed.',
      fields: [],
      traceId: 't',
    })

    renderWithProviders(<RegistrationForm />)
    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /create account/i }))

    expect(await screen.findByText('Registration could not be completed.')).toBeInTheDocument()
  })

  it('stores the session on success without a separate login step', async () => {
    vi.spyOn(authApi, 'register').mockResolvedValue({
      user,
      accessToken: 'new-access',
      refreshToken: 'new-refresh',
    })

    renderWithProviders(<RegistrationForm />)
    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /create account/i }))

    await waitFor(() => expect(useAuthStore.getState().status).toBe('authenticated'))
    expect(useAuthStore.getState().user).toEqual(user)
  })

  it('navigates to /dashboard on success (real route assertion, not just store state)', async () => {
    vi.spyOn(authApi, 'register').mockResolvedValue({
      user,
      accessToken: 'new-access',
      refreshToken: 'new-refresh',
    })
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/register']}>
          <Routes>
            <Route path="/register" element={<RegistrationForm />} />
            <Route path="/dashboard" element={<div>dashboard stub</div>} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    )

    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /create account/i }))

    expect(await screen.findByText('dashboard stub')).toBeInTheDocument()
  })
})
