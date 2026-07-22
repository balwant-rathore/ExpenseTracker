import type { EmployeeRole } from '@/types/auth'
import type { DashboardResponse } from '../types/dashboard'

interface DashboardMetricsProps {
  role: EmployeeRole
  dashboard: DashboardResponse
}

interface MetricTile {
  label: string
  value: number
}

export function DashboardMetrics({ role, dashboard }: DashboardMetricsProps) {
  const tiles: MetricTile[] = [
    { label: 'Total Submitted', value: dashboard.totalSubmitted },
    { label: 'Approved', value: dashboard.approved },
    { label: 'Reimbursed', value: dashboard.reimbursed },
  ]

  if (role === 'Manager' || role === 'Finance') {
    tiles.push({ label: 'Pending Approvals', value: (dashboard as { pendingApprovals: number }).pendingApprovals })
  }

  if (role === 'Finance') {
    tiles.push({
      label: 'Pending Reimbursements',
      value: (dashboard as { pendingReimbursements: number }).pendingReimbursements,
    })
  }

  return (
    <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
      {tiles.map((tile) => (
        <div key={tile.label} className="rounded-lg border p-4">
          <dt className="text-sm text-muted-foreground">{tile.label}</dt>
          <dd className="text-2xl font-medium">{tile.value}</dd>
        </div>
      ))}
    </div>
  )
}
