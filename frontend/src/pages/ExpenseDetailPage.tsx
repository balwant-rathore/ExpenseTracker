import { useParams } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import { useExpense } from '@/features/expenses/api/useExpense'
import { ExpenseDetail } from '@/features/expenses/components/ExpenseDetail'

export function ExpenseDetailPage() {
  const { id } = useParams<{ id: string }>()
  const user = useAuthStore((state) => state.user)
  const { data, isLoading, error } = useExpense(id ?? '')

  if (isLoading) {
    return <div className="p-4">Loading…</div>
  }

  if (error?.code === 'RESOURCE_NOT_FOUND') {
    return <div className="p-4">This expense could not be found.</div>
  }

  if (error?.code === 'AUTHORIZATION_FAILED') {
    return <div className="p-4">You do not have permission to view this expense.</div>
  }

  if (!data) {
    return <div className="p-4">This expense could not be loaded.</div>
  }

  return (
    <div className="mx-auto max-w-2xl p-4">
      <ExpenseDetail
        expense={data.expense}
        currentUserEmployeeNumber={user?.employeeNumber}
        currentUserRole={user?.role}
      />
    </div>
  )
}
