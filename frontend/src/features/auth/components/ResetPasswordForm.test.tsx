import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import * as authApi from '../api/authApi'
import { renderWithProviders } from '@/test/renderWithProviders'
import { ResetPasswordForm } from './ResetPasswordForm'

function fillValidForm() {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
  fireEvent.change(screen.getByLabelText('6-digit code'), { target: { value: '123456' } })
  fireEvent.change(screen.getByLabelText('New password'), { target: { value: 'newpass1' } })
  fireEvent.change(screen.getByLabelText('Confirm new password'), { target: { value: 'newpass1' } })
}

describe('ResetPasswordForm', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('calls reset-password with exactly email, otp, and newPassword — never confirmNewPassword', async () => {
    const resetPasswordSpy = vi.spyOn(authApi, 'resetPassword').mockResolvedValue(undefined)

    renderWithProviders(<ResetPasswordForm />)
    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /reset password/i }))

    await vi.waitFor(() =>
      expect(resetPasswordSpy).toHaveBeenCalledWith({
        email: 'a@b.com',
        otp: '123456',
        newPassword: 'newpass1',
      }),
    )
  })

  it('navigates to /login with a success message after a successful reset', async () => {
    vi.spyOn(authApi, 'resetPassword').mockResolvedValue(undefined)
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/reset-password']}>
          <Routes>
            <Route path="/reset-password" element={<ResetPasswordForm />} />
            <Route path="/login" element={<div>login page stub</div>} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    )

    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /reset password/i }))

    expect(await screen.findByText('login page stub')).toBeInTheDocument()
  })

  it('blocks submission when confirmNewPassword does not match newPassword', async () => {
    const resetPasswordSpy = vi.spyOn(authApi, 'resetPassword')

    renderWithProviders(<ResetPasswordForm />)
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
    fireEvent.change(screen.getByLabelText('6-digit code'), { target: { value: '123456' } })
    fireEvent.change(screen.getByLabelText('New password'), { target: { value: 'newpass1' } })
    fireEvent.change(screen.getByLabelText('Confirm new password'), { target: { value: 'different1' } })
    fireEvent.click(screen.getByRole('button', { name: /reset password/i }))

    expect(await screen.findByText('Passwords do not match.')).toBeInTheDocument()
    expect(resetPasswordSpy).not.toHaveBeenCalled()
  })

  it('shows an expiry message with a link back to forgot-password on a 410 response', async () => {
    vi.spyOn(authApi, 'resetPassword').mockRejectedValue({
      code: 'RESOURCE_EXPIRED',
      message: 'This one-time code has expired.',
      fields: [],
      traceId: 't',
    })

    renderWithProviders(<ResetPasswordForm />)
    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /reset password/i }))

    expect(await screen.findByText('This one-time code has expired.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /request a new code/i })).toHaveAttribute(
      'href',
      '/forgot-password',
    )
  })

  it('shows the generic invalid/used-OTP message on a 401 response', async () => {
    vi.spyOn(authApi, 'resetPassword').mockRejectedValue({
      code: 'AUTHENTICATION_FAILED',
      message: 'This one-time code is invalid.',
      fields: [],
      traceId: 't',
    })

    renderWithProviders(<ResetPasswordForm />)
    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /reset password/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('This one-time code is invalid.')
  })

  it('shows field-level detail on newPassword for a 400 response', async () => {
    vi.spyOn(authApi, 'resetPassword').mockRejectedValue({
      code: 'VALIDATION_ERROR',
      message: 'Password does not meet complexity requirements.',
      fields: ['newPassword'],
      traceId: 't',
    })

    renderWithProviders(<ResetPasswordForm />)
    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /reset password/i }))

    expect(
      await screen.findByText('Password does not meet complexity requirements.'),
    ).toBeInTheDocument()
  })

  it('shows a generic throttling message on a 429 response', async () => {
    // FRS §3.5.4: reset-password is rate-limited the same as the other three auth endpoints —
    // gap caught by /review (the original spec delta never listed this scenario).
    vi.spyOn(authApi, 'resetPassword').mockRejectedValue({
      code: 'RATE_LIMIT_EXCEEDED',
      message: 'Too many requests.',
      fields: [],
      traceId: 't',
    })

    renderWithProviders(<ResetPasswordForm />)
    fillValidForm()
    fireEvent.click(screen.getByRole('button', { name: /reset password/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Too many attempts')
  })
})
