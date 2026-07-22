import { z } from 'zod'

export const resetPasswordSchema = z
  .object({
    email: z.email({ error: 'Enter a valid email address.' }),
    otp: z.string().regex(/^\d{6}$/, { error: 'Enter the 6-digit code.' }),
    newPassword: z
      .string()
      .min(8, { error: 'Password must be at least 8 characters.' })
      .regex(/[A-Za-z]/, { error: 'Password must contain at least one letter.' })
      .regex(/\d/, { error: 'Password must contain at least one number.' }),
    confirmNewPassword: z.string().min(1, { error: 'Confirm your new password.' }),
  })
  .refine((data) => data.newPassword === data.confirmNewPassword, {
    error: 'Passwords do not match.',
    path: ['confirmNewPassword'],
  })

export type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>
