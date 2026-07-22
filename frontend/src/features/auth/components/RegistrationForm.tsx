import { useForm, type FieldError as RhfFieldError } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import type { ApiError } from '@/lib/apiClient'
import { useRegister } from '../api/useRegister'
import { registrationSchema, type RegistrationFormValues } from '../schemas/registrationSchema'
import { hasFieldError } from '../utils/hasFieldError'

type ApiField = 'employeeNumber' | 'email' | 'password'

function fieldMessages(
  field: ApiField,
  formError: RhfFieldError | undefined,
  apiError: ApiError | null,
): { message?: string }[] | undefined {
  if (formError) {
    return [formError]
  }
  if (!apiError) {
    return undefined
  }
  if (field === 'employeeNumber' && apiError.code === 'BUSINESS_RULE_VIOLATION') {
    return [{ message: apiError.message }]
  }
  if (apiError.code === 'VALIDATION_ERROR' && hasFieldError(apiError.fields, field)) {
    return [{ message: apiError.message }]
  }
  return undefined
}

export function RegistrationForm() {
  const navigate = useNavigate()
  const {
    register: registerField,
    handleSubmit,
    formState: { errors },
  } = useForm<RegistrationFormValues>({ resolver: zodResolver(registrationSchema) })
  const registerMutation = useRegister()

  const onSubmit = handleSubmit(({ confirmPassword: _confirmPassword, ...input }) => {
    registerMutation.mutate(input, {
      onSuccess: () => navigate('/dashboard'),
    })
  })

  const apiError = registerMutation.error
  const conflictMessage = apiError?.code === 'RESOURCE_CONFLICT' ? apiError.message : null

  return (
    <form onSubmit={onSubmit} noValidate>
      <FieldGroup>
        <Field data-invalid={!!fieldMessages('employeeNumber', errors.employeeNumber, apiError)}>
          <FieldLabel htmlFor="register-employee-number">Employee number</FieldLabel>
          <Input
            id="register-employee-number"
            autoComplete="off"
            aria-invalid={!!errors.employeeNumber}
            {...registerField('employeeNumber')}
          />
          <FieldError errors={fieldMessages('employeeNumber', errors.employeeNumber, apiError)} />
        </Field>
        <Field data-invalid={!!fieldMessages('email', errors.email, apiError)}>
          <FieldLabel htmlFor="register-email">Email</FieldLabel>
          <Input
            id="register-email"
            type="email"
            autoComplete="email"
            aria-invalid={!!errors.email}
            {...registerField('email')}
          />
          <FieldError errors={fieldMessages('email', errors.email, apiError)} />
        </Field>
        <Field data-invalid={!!fieldMessages('password', errors.password, apiError)}>
          <FieldLabel htmlFor="register-password">Password</FieldLabel>
          <Input
            id="register-password"
            type="password"
            autoComplete="new-password"
            aria-invalid={!!errors.password}
            {...registerField('password')}
          />
          <FieldError errors={fieldMessages('password', errors.password, apiError)} />
        </Field>
        <Field data-invalid={!!errors.confirmPassword}>
          <FieldLabel htmlFor="register-confirm-password">Confirm password</FieldLabel>
          <Input
            id="register-confirm-password"
            type="password"
            autoComplete="new-password"
            aria-invalid={!!errors.confirmPassword}
            {...registerField('confirmPassword')}
          />
          <FieldError errors={errors.confirmPassword ? [errors.confirmPassword] : undefined} />
        </Field>
        {conflictMessage && (
          <p role="alert" className="text-sm text-destructive">
            {conflictMessage}
          </p>
        )}
        <Button type="submit" disabled={registerMutation.isPending}>
          {registerMutation.isPending ? 'Creating account…' : 'Create account'}
        </Button>
      </FieldGroup>
    </form>
  )
}
