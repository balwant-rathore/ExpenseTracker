import { Navigate } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import { useBreadcrumb } from '@/store/useBreadcrumb'
import { useDashboard } from '@/features/dashboard/api/useDashboard'
import { DashboardMetrics } from '@/features/dashboard/components/DashboardMetrics'

export function DashboardPage() {
  const role = useAuthStore((state) => state.user?.role)
  const isCompliance = role === 'ComplianceOfficer'

  useBreadcrumb('Dashboard', !isCompliance)
  const { data, isLoading, error } = useDashboard(!isCompliance)

  if (isCompliance) {
    return <Navigate to="/expenses" replace />
  }

  if (isLoading) {
    return <div className="p-4">Loading…</div>
  }

  if (error || !data) {
    return <div className="p-4">This dashboard could not be loaded.</div>
  }

  return (
    <div className="flex flex-col gap-4 p-4">
      <h1 className="text-xl font-medium">Dashboard</h1>
      <DashboardMetrics role={role!} dashboard={data} />
    </div>
  )
}
