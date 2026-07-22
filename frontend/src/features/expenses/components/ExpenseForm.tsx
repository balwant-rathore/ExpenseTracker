import { useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { EXPENSE_CATEGORIES, type ExpenseResponse } from '@/types/expense'
import { categoryLabel } from '../utils/expenseDisplay'
import { expenseFormSchema, type ExpenseFormValues } from '../schemas/expenseFormSchema'
import { AttachmentPicker, type AttachmentSelection } from './AttachmentPicker'
import { useUploadAttachment } from '../api/useUploadAttachment'
import { useCreateExpense } from '../api/useCreateExpense'
import { useUpdateExpense } from '../api/useUpdateExpense'

type ExpenseFormProps = { mode: 'create' } | { mode: 'edit'; expense: ExpenseResponse }

export function ExpenseForm(props: ExpenseFormProps) {
  const navigate = useNavigate()
  const isEdit = props.mode === 'edit'
  const expense = isEdit ? props.expense : undefined

  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<ExpenseFormValues>({
    resolver: zodResolver(expenseFormSchema),
    defaultValues: expense
      ? {
          expenseDate: expense.expenseDate,
          category: expense.category,
          amount: String(expense.amount),
          description: expense.description,
        }
      : undefined,
  })

  const [attachment, setAttachment] = useState<AttachmentSelection>(
    expense
      ? {
          kind: 'existing',
          attachmentId: expense.receiptAttachmentId,
          fileName: expense.attachmentOriginalFileName ?? 'Receipt',
        }
      : { kind: 'none' },
  )
  const [attachmentError, setAttachmentError] = useState<string | null>(null)
  const [uploadError, setUploadError] = useState<string | null>(null)

  const uploadMutation = useUploadAttachment()
  const createMutation = useCreateExpense()
  const updateMutation = useUpdateExpense(expense?.id ?? '')
  const mutation = isEdit ? updateMutation : createMutation

  async function resolveAttachmentId(): Promise<string | null> {
    if (attachment.kind === 'existing') {
      return attachment.attachmentId
    }
    if (attachment.kind === 'new') {
      try {
        const result = await uploadMutation.mutateAsync(attachment.file)
        return result.attachmentId
      } catch {
        setUploadError('Failed to upload the receipt. Please try again.')
        return null
      }
    }
    setAttachmentError('A receipt attachment is required.')
    return null
  }

  function onSave(action: 'Draft' | 'Submit') {
    return handleSubmit(async (values) => {
      setAttachmentError(null)
      setUploadError(null)

      const receiptAttachmentId = await resolveAttachmentId()
      if (!receiptAttachmentId) {
        return
      }

      const payload = {
        expenseDate: values.expenseDate,
        category: values.category,
        amount: Number(values.amount),
        currency: 'INR' as const,
        description: values.description,
        receiptAttachmentId,
      }

      if (isEdit) {
        updateMutation.mutate(payload, {
          onSuccess: (data) => navigate(`/expenses/${data.expense.id}`),
        })
      } else {
        createMutation.mutate(
          { ...payload, action },
          { onSuccess: (data) => navigate(`/expenses/${data.expense.id}`) },
        )
      }
    })
  }

  const backendErrorMessage = uploadError ?? mutation.error?.message ?? null

  return (
    <form noValidate>
      <FieldGroup>
        <Field data-invalid={!!errors.expenseDate}>
          <FieldLabel htmlFor="expense-date">Expense date</FieldLabel>
          <Input
            id="expense-date"
            type="date"
            aria-invalid={!!errors.expenseDate}
            {...register('expenseDate')}
          />
          <FieldError errors={errors.expenseDate ? [errors.expenseDate] : undefined} />
        </Field>

        <Field data-invalid={!!errors.category}>
          <FieldLabel htmlFor="expense-category">Category</FieldLabel>
          <Controller
            control={control}
            name="category"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="expense-category" aria-invalid={!!errors.category}>
                  <SelectValue placeholder="Select category" />
                </SelectTrigger>
                <SelectContent>
                  {EXPENSE_CATEGORIES.map((category) => (
                    <SelectItem key={category} value={category}>
                      {categoryLabel(category)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          <FieldError errors={errors.category ? [errors.category] : undefined} />
        </Field>

        <Field data-invalid={!!errors.amount}>
          <FieldLabel htmlFor="expense-amount">Amount</FieldLabel>
          <Input
            id="expense-amount"
            type="number"
            step="0.01"
            aria-invalid={!!errors.amount}
            {...register('amount')}
          />
          <FieldError errors={errors.amount ? [errors.amount] : undefined} />
        </Field>

        <Field>
          <FieldLabel htmlFor="expense-currency">Currency</FieldLabel>
          <Input id="expense-currency" value="INR" disabled readOnly />
        </Field>

        <Field data-invalid={!!errors.description}>
          <FieldLabel htmlFor="expense-description">Description</FieldLabel>
          <Textarea
            id="expense-description"
            aria-invalid={!!errors.description}
            {...register('description')}
          />
          <FieldError errors={errors.description ? [errors.description] : undefined} />
        </Field>

        <Field>
          <FieldLabel>Receipt</FieldLabel>
          <AttachmentPicker
            value={attachment}
            onChange={(next) => {
              setAttachment(next)
              setAttachmentError(null)
            }}
            error={attachmentError ?? undefined}
          />
        </Field>

        {backendErrorMessage && (
          <p role="alert" className="text-sm text-destructive">
            {backendErrorMessage}
          </p>
        )}

        {isEdit ? (
          <Button type="button" onClick={onSave('Draft')} disabled={mutation.isPending}>
            {mutation.isPending ? 'Saving…' : 'Save changes'}
          </Button>
        ) : (
          <div className="flex gap-2">
            <Button type="button" variant="outline" onClick={onSave('Draft')} disabled={mutation.isPending}>
              {mutation.isPending ? 'Saving…' : 'Save as Draft'}
            </Button>
            <Button type="button" onClick={onSave('Submit')} disabled={mutation.isPending}>
              {mutation.isPending ? 'Submitting…' : 'Submit'}
            </Button>
          </div>
        )}
      </FieldGroup>
    </form>
  )
}
