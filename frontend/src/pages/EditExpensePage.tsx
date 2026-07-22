import { useParams } from 'react-router-dom'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useExpense } from '@/features/expenses/api/useExpense'
import { ExpenseForm } from '@/features/expenses/components/ExpenseForm'

export function EditExpensePage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading, isError } = useExpense(id ?? '')

  if (isLoading) {
    return <div className="p-4">Loading…</div>
  }

  if (isError || !data) {
    return <div className="p-4">This expense could not be loaded.</div>
  }

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-4 p-4">
      <Card>
        <CardHeader>
          <CardTitle>Edit expense {data.expense.expenseNumber}</CardTitle>
          <CardDescription>Update the details or replace the receipt.</CardDescription>
        </CardHeader>
        <CardContent>
          <ExpenseForm mode="edit" expense={data.expense} />
        </CardContent>
      </Card>
    </div>
  )
}
