import { Link, Outlet } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/store/authStore'
import { useLogout } from '@/features/auth/api/useLogout'
import { Breadcrumbs } from '@/components/Breadcrumbs'

/**
 * Minimal authenticated shell so a user can actually reach /expenses and
 * back — a full navigation/IA redesign is out of scope for ET017 (design.md D8).
 */
export function AppLayout() {
  const logoutMutation = useLogout()
  const role = useAuthStore((state) => state.user?.role)

  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex items-center justify-between border-b p-4">
        <nav className="flex gap-4">
          {role === 'ComplianceOfficer' ? (
            <Link to="/expenses" className="text-sm font-medium underline-offset-4 hover:underline">
              My Expenses
            </Link>
          ) : (
            <>
              <Link to="/dashboard" className="text-sm font-medium underline-offset-4 hover:underline">
                Dashboard
              </Link>
              <Link to="/expenses" className="text-sm font-medium underline-offset-4 hover:underline">
                My Expenses
              </Link>
            </>
          )}
          {role === 'Finance' && (
            <>
              <Link
                to="/finance/search"
                className="text-sm font-medium underline-offset-4 hover:underline"
              >
                Finance Search
              </Link>
              <Link
                to="/reports/monthly-reimbursement"
                className="text-sm font-medium underline-offset-4 hover:underline"
              >
                Monthly Report
              </Link>
            </>
          )}
        </nav>
        <Button
          variant="outline"
          onClick={() => logoutMutation.mutate()}
          disabled={logoutMutation.isPending}
        >
          {logoutMutation.isPending ? 'Logging out…' : 'Log out'}
        </Button>
      </header>
      <Breadcrumbs />
      <main className="flex-1">
        <Outlet />
      </main>
    </div>
  )
}
