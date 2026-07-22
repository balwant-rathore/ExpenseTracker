import { z } from 'zod'
import { EXPENSE_CATEGORIES } from '@/types/expense'

function todayIsoDate(): string {
  return new Date().toISOString().slice(0, 10)
}

export const expenseFormSchema = z.object({
  expenseDate: z
    .string()
    .min(1, { error: 'Expense date is required.' })
    .refine((value) => value <= todayIsoDate(), {
      error: 'Expense date cannot be in the future.',
    }),
  category: z.enum(EXPENSE_CATEGORIES, { error: 'Select a valid category.' }),
  amount: z
    .string()
    .min(1, { error: 'Amount is required.' })
    .refine((value) => !Number.isNaN(Number(value)), { error: 'Amount must be a number.' })
    .refine((value) => Number(value) > 0, { error: 'Amount must be greater than zero.' }),
  description: z
    .string()
    .min(1, { error: 'Description is required.' })
    .max(500, { error: 'Description must not exceed 500 characters.' }),
})

export type ExpenseFormValues = z.infer<typeof expenseFormSchema>
