import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Field, FieldError, FieldLabel } from '@/components/ui/field'
import { Textarea } from '@/components/ui/textarea'
import { rejectionCommentSchema, type RejectionCommentInput } from '../schemas/rejectionCommentSchema'
import { useRejectExpense } from '../api/useRejectExpense'
import { useComplianceReject } from '../api/useComplianceReject'

interface RejectionDialogProps {
  expenseId: string
  action: 'managerReject' | 'complianceReject'
  onRejected?: () => void
}

export function RejectionDialog({ expenseId, action, onRejected }: RejectionDialogProps) {
  const [open, setOpen] = useState(false)
  const managerRejectMutation = useRejectExpense(expenseId)
  const complianceRejectMutation = useComplianceReject(expenseId)
  const mutation = action === 'managerReject' ? managerRejectMutation : complianceRejectMutation

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isValid },
  } = useForm<RejectionCommentInput>({
    resolver: zodResolver(rejectionCommentSchema),
    mode: 'onChange',
    defaultValues: { rejectionComment: '' },
  })

  function handleOpenChange(next: boolean) {
    setOpen(next)
    if (!next) {
      reset()
    }
  }

  function onConfirm(values: RejectionCommentInput) {
    mutation.mutate(values.rejectionComment, {
      onSuccess: () => {
        setOpen(false)
        reset()
        onRejected?.()
      },
    })
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger render={<Button variant="destructive">Reject</Button>} />
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Reject this expense?</DialogTitle>
          <DialogDescription>
            Provide a comment explaining the rejection. The employee will see this comment.
          </DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={handleSubmit(onConfirm)}>
          <Field data-invalid={!!errors.rejectionComment}>
            <FieldLabel htmlFor="rejection-comment">Rejection comment</FieldLabel>
            <Textarea
              id="rejection-comment"
              aria-invalid={!!errors.rejectionComment}
              {...register('rejectionComment')}
            />
            <FieldError errors={errors.rejectionComment ? [errors.rejectionComment] : undefined} />
          </Field>

          {mutation.error && (
            <p role="alert" className="text-sm text-destructive">
              {mutation.error.message}
            </p>
          )}

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => handleOpenChange(false)}
              disabled={mutation.isPending}
            >
              Keep expense
            </Button>
            <Button type="submit" variant="destructive" disabled={!isValid || mutation.isPending}>
              {mutation.isPending ? 'Rejecting…' : 'Confirm rejection'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
