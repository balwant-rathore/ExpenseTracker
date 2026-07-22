import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'
import { useBreadcrumbStore } from './breadcrumbStore'

export function useBreadcrumb(label: string, enabled = true): void {
  const location = useLocation()
  const push = useBreadcrumbStore((state) => state.push)

  useEffect(() => {
    // `enabled` lets a page skip pushing itself (e.g. DashboardPage redirecting a
    // ComplianceOfficer away before it ever really renders) without breaking the rules of
    // hooks by calling useBreadcrumb conditionally.
    if (!enabled) {
      return
    }
    push({ label, path: location.pathname })
    // Runs when the path/label this page identifies itself with changes — pushing on every
    // render would append a duplicate entry on state-only re-renders (e.g. a refetch).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.pathname, label, enabled])
}
