import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/store/authStore'
import { useLogout } from '@/features/auth/api/useLogout'

/**
 * Minimal authenticated placeholder — ET019 replaces this with the real
 * role-specific dashboard. Exists so login/register/refresh and the route
 * guards have a real destination to redirect to and to be tested against.
 */
export function DashboardPage() {
  const user = useAuthStore((state) => state.user)
  const logoutMutation = useLogout()

  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-4 p-4">
      <h1 className="text-2xl font-medium">Welcome, {user?.firstName}</h1>
      <Button onClick={() => logoutMutation.mutate()} disabled={logoutMutation.isPending}>
        {logoutMutation.isPending ? 'Logging out…' : 'Log out'}
      </Button>
    </div>
  )
}
