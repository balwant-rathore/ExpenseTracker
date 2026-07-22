import { z } from 'zod'

export const registrationSchema = z
  .object({
    employeeNumber: z.string().min(1, { error: 'Employee number is required.' }),
    email: z.email({ error: 'Enter a valid email address.' }),
    password: z
      .string()
      .min(8, { error: 'Password must be at least 8 characters.' })
      .regex(/[A-Za-z]/, { error: 'Password must contain at least one letter.' })
      .regex(/\d/, { error: 'Password must contain at least one number.' }),
    confirmPassword: z.string().min(1, { error: 'Confirm your password.' }),
  })
  .refine((data) => data.password === data.confirmPassword, {
    error: 'Passwords do not match.',
    path: ['confirmPassword'],
  })

export type RegistrationFormValues = z.infer<typeof registrationSchema>
