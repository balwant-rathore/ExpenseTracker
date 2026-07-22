import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import type { EmployeeRole } from '@/types/auth'

interface RequireRoleProps {
  allowedRoles: EmployeeRole[]
  children: ReactNode
  redirectTo?: string
}

/**
 * Role restriction, composed alongside ProtectedRoute (which must run first
 * to guarantee `user` is populated). Reads only the authenticated user's
 * `role` from the store — never decodes the JWT itself, per AGENTS.md
 * "never trust roles from the JWT" (the access token carries only `sub`).
 */
export function RequireRole({ allowedRoles, children, redirectTo = '/dashboard' }: RequireRoleProps) {
  const role = useAuthStore((state) => state.user?.role)

  if (!role || !allowedRoles.includes(role)) {
    return <Navigate to={redirectTo} replace />
  }

  return <>{children}</>
}
