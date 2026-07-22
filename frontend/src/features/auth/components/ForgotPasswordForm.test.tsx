import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, screen } from '@testing-library/react'
import * as authApi from '../api/authApi'
import { renderWithProviders } from '@/test/renderWithProviders'
import { ForgotPasswordForm } from './ForgotPasswordForm'

describe('ForgotPasswordForm', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('shows the same generic confirmation for any submitted email', async () => {
    const forgotPasswordSpy = vi.spyOn(authApi, 'forgotPassword').mockResolvedValue(undefined)

    renderWithProviders(<ForgotPasswordForm />)
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'anyone@company.com' } })
    fireEvent.click(screen.getByRole('button', { name: /send reset code/i }))

    expect(
      await screen.findByText('If an account exists for that email, a one-time code has been issued.'),
    ).toBeInTheDocument()
    expect(forgotPasswordSpy).toHaveBeenCalledWith('anyone@company.com')
  })

  it('shows a generic throttling message on a 429 response', async () => {
    vi.spyOn(authApi, 'forgotPassword').mockRejectedValue({
      code: 'RATE_LIMIT_EXCEEDED',
      message: 'Too many requests.',
      fields: [],
      traceId: 't',
    })

    renderWithProviders(<ForgotPasswordForm />)
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'anyone@company.com' } })
    fireEvent.click(screen.getByRole('button', { name: /send reset code/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Too many attempts')
  })
})
