import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Button, buttonVariants } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { useResetPassword } from '../api/useResetPassword'
import { resetPasswordSchema, type ResetPasswordFormValues } from '../schemas/resetPasswordSchema'
import { hasFieldError } from '../utils/hasFieldError'

export function ResetPasswordForm() {
  const navigate = useNavigate()
  const location = useLocation()
  const prefillEmail = (location.state as { email?: string } | null)?.email ?? ''

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { email: prefillEmail, otp: '', newPassword: '', confirmNewPassword: '' },
  })
  const resetPasswordMutation = useResetPassword()

  const onSubmit = handleSubmit(({ confirmNewPassword: _confirmNewPassword, ...input }) => {
    resetPasswordMutation.mutate(input, {
      onSuccess: () => {
        navigate('/login', { state: { message: 'Your password has been reset. Please log in.' } })
      },
    })
  })

  const apiError = resetPasswordMutation.error
  const expiredMessage = apiError?.code === 'RESOURCE_EXPIRED' ? apiError.message : null
  const invalidOtpMessage = apiError?.code === 'AUTHENTICATION_FAILED' ? apiError.message : null
  const rateLimitMessage =
    apiError?.code === 'RATE_LIMIT_EXCEEDED' ? 'Too many attempts. Please try again later.' : null
  const newPasswordApiError =
    apiError?.code === 'VALIDATION_ERROR' && hasFieldError(apiError.fields, 'newPassword')
      ? { message: apiError.message }
      : undefined

  return (
    <form onSubmit={onSubmit} noValidate>
      <FieldGroup>
        <Field data-invalid={!!errors.email}>
          <FieldLabel htmlFor="reset-password-email">Email</FieldLabel>
          <Input
            id="reset-password-email"
            type="email"
            autoComplete="email"
            aria-invalid={!!errors.email}
            {...register('email')}
          />
          <FieldError errors={errors.email ? [errors.email] : undefined} />
        </Field>
        <Field data-invalid={!!errors.otp}>
          <FieldLabel htmlFor="reset-password-otp">6-digit code</FieldLabel>
          <Input
            id="reset-password-otp"
            inputMode="numeric"
            autoComplete="one-time-code"
            aria-invalid={!!errors.otp}
            {...register('otp')}
          />
          <FieldError errors={errors.otp ? [errors.otp] : undefined} />
        </Field>
        <Field data-invalid={!!errors.newPassword || !!newPasswordApiError}>
          <FieldLabel htmlFor="reset-password-new-password">New password</FieldLabel>
          <Input
            id="reset-password-new-password"
            type="password"
            autoComplete="new-password"
            aria-invalid={!!errors.newPassword}
            {...register('newPassword')}
          />
          <FieldError
            errors={errors.newPassword ? [errors.newPassword] : newPasswordApiError ? [newPasswordApiError] : undefined}
          />
        </Field>
        <Field data-invalid={!!errors.confirmNewPassword}>
          <FieldLabel htmlFor="reset-password-confirm-new-password">Confirm new password</FieldLabel>
          <Input
            id="reset-password-confirm-new-password"
            type="password"
            autoComplete="new-password"
            aria-invalid={!!errors.confirmNewPassword}
            {...register('confirmNewPassword')}
          />
          <FieldError errors={errors.confirmNewPassword ? [errors.confirmNewPassword] : undefined} />
        </Field>
        {expiredMessage && (
          <div role="alert" className="flex flex-col gap-2 text-sm text-destructive">
            <p>{expiredMessage}</p>
            <Link to="/forgot-password" className={buttonVariants({ variant: 'link' })}>
              Request a new code
            </Link>
          </div>
        )}
        {invalidOtpMessage && (
          <p role="alert" className="text-sm text-destructive">
            {invalidOtpMessage}
          </p>
        )}
        {rateLimitMessage && (
          <p role="alert" className="text-sm text-destructive">
            {rateLimitMessage}
          </p>
        )}
        <Button type="submit" disabled={resetPasswordMutation.isPending}>
          {resetPasswordMutation.isPending ? 'Resetting…' : 'Reset password'}
        </Button>
      </FieldGroup>
    </form>
  )
}
