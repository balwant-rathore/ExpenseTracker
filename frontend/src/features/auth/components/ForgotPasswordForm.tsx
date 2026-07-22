import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from 'react-router-dom'
import { Button, buttonVariants } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import type { ApiError } from '@/lib/apiClient'
import { useForgotPassword } from '../api/useForgotPassword'
import { forgotPasswordSchema, type ForgotPasswordFormValues } from '../schemas/forgotPasswordSchema'

function genericErrorMessage(error: ApiError | null): string | null {
  if (error?.code === 'RATE_LIMIT_EXCEEDED') {
    return 'Too many attempts. Please try again later.'
  }
  return null
}

export function ForgotPasswordForm() {
  const {
    register,
    handleSubmit,
    getValues,
    formState: { errors },
  } = useForm<ForgotPasswordFormValues>({ resolver: zodResolver(forgotPasswordSchema) })
  const forgotPasswordMutation = useForgotPassword()

  const onSubmit = handleSubmit((values) => {
    forgotPasswordMutation.mutate(values.email)
  })

  if (forgotPasswordMutation.isSuccess) {
    return (
      <div className="flex flex-col gap-4">
        <p role="status">
          If an account exists for that email, a one-time code has been issued.
        </p>
        <Link
          to="/reset-password"
          state={{ email: getValues('email') }}
          className={buttonVariants()}
        >
          Enter code
        </Link>
      </div>
    )
  }

  const errorMessage = genericErrorMessage(forgotPasswordMutation.error)

  return (
    <form onSubmit={onSubmit} noValidate>
      <FieldGroup>
        <Field data-invalid={!!errors.email}>
          <FieldLabel htmlFor="forgot-password-email">Email</FieldLabel>
          <Input
            id="forgot-password-email"
            type="email"
            autoComplete="email"
            aria-invalid={!!errors.email}
            {...register('email')}
          />
          <FieldError errors={errors.email ? [errors.email] : undefined} />
        </Field>
        {errorMessage && (
          <p role="alert" className="text-sm text-destructive">
            {errorMessage}
          </p>
        )}
        <Button type="submit" disabled={forgotPasswordMutation.isPending}>
          {forgotPasswordMutation.isPending ? 'Sending…' : 'Send reset code'}
        </Button>
      </FieldGroup>
    </form>
  )
}
