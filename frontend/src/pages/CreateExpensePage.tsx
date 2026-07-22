import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { ExpenseForm } from '@/features/expenses/components/ExpenseForm'

export function CreateExpensePage() {
  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-4 p-4">
      <Card>
        <CardHeader>
          <CardTitle>New expense</CardTitle>
          <CardDescription>Fill in the details and attach your receipt.</CardDescription>
        </CardHeader>
        <CardContent>
          <ExpenseForm mode="create" />
        </CardContent>
      </Card>
    </div>
  )
}
