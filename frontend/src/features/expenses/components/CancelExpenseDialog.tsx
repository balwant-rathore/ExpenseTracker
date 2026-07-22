import { useState } from 'react'
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
import { useCancelExpense } from '../api/useCancelExpense'

interface CancelExpenseDialogProps {
  expenseId: string
  onCancelled?: () => void
}

export function CancelExpenseDialog({ expenseId, onCancelled }: CancelExpenseDialogProps) {
  const [open, setOpen] = useState(false)
  const cancelMutation = useCancelExpense(expenseId)

  function handleConfirm() {
    cancelMutation.mutate(undefined, {
      onSuccess: () => {
        setOpen(false)
        onCancelled?.()
      },
    })
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger render={<Button variant="destructive">Cancel expense</Button>} />
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Cancel this expense?</DialogTitle>
          <DialogDescription>
            This cannot be undone. You will need to submit a new expense if you change your mind.
          </DialogDescription>
        </DialogHeader>
        {cancelMutation.error && (
          <p role="alert" className="text-sm text-destructive">
            {cancelMutation.error.message}
          </p>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => setOpen(false)} disabled={cancelMutation.isPending}>
            Keep expense
          </Button>
          <Button variant="destructive" onClick={handleConfirm} disabled={cancelMutation.isPending}>
            {cancelMutation.isPending ? 'Cancelling…' : 'Yes, cancel it'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
