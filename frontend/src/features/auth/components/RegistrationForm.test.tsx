import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
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
})
