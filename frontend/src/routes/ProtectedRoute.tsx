import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'

/**
 * Authentication-only gate. Role-specific restrictions live in RequireRole,
 * composed separately so pages that only need "must be logged in" don't pay
 * for a role check they don't need.
 */
export function ProtectedRoute({ children }: { children: ReactNode }) {
  const status = useAuthStore((state) => state.status)

  if (status === 'bootstrapping') {
    return (
      <div role="status" className="flex min-h-svh items-center justify-center">
        Loading…
      </div>
    )
  }

  if (status === 'unauthenticated') {
    return <Navigate to="/login" replace />
  }

  return <>{children}</>
}
