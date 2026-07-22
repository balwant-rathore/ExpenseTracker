import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import type { User } from '@/types/auth'
import type { EmployeeDashboard, FinanceDashboard, ManagerDashboard } from '@/features/dashboard/types/dashboard'
import * as dashboardApi from '@/features/dashboard/api/dashboardApi'
import { DashboardPage } from './DashboardPage'

function renderDashboard() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/dashboard']}>
        <Routes>
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/expenses" element={<div>expenses stub</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function setUser(role: User['role']) {
  useAuthStore.setState({
    user: { id: 'u1', email: 'a@b.com', employeeNumber: 'EMP1', firstName: 'Ada', lastName: 'Lovelace', role },
    accessToken: 'token',
    status: 'authenticated',
  })
}

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  // Scenario: Employee sees three metrics
  it('renders three metrics for Employee', async () => {
    setUser('Employee')
    const dashboard: EmployeeDashboard = { totalSubmitted: 2, approved: 1, reimbursed: 3 }
    vi.spyOn(dashboardApi, 'getDashboard').mockResolvedValue(dashboard)

    renderDashboard()

    expect(await screen.findByText('Total Submitted')).toBeInTheDocument()
    expect(screen.getByText('Approved')).toBeInTheDocument()
    expect(screen.getByText('Reimbursed')).toBeInTheDocument()
    expect(screen.queryByText('Pending Approvals')).not.toBeInTheDocument()
    expect(screen.queryByText('Pending Reimbursements')).not.toBeInTheDocument()
  })

  // Scenario: Manager sees four metrics
  it('renders four metrics for Manager', async () => {
    setUser('Manager')
    const dashboard: ManagerDashboard = { totalSubmitted: 2, approved: 1, reimbursed: 3, pendingApprovals: 4 }
    vi.spyOn(dashboardApi, 'getDashboard').mockResolvedValue(dashboard)

    renderDashboard()

    expect(await screen.findByText('Pending Approvals')).toBeInTheDocument()
    expect(screen.queryByText('Pending Reimbursements')).not.toBeInTheDocument()
  })

  // Scenario: Finance sees five metrics
  it('renders five metrics for Finance', async () => {
    setUser('Finance')
    const dashboard: FinanceDashboard = {
      totalSubmitted: 2,
      approved: 1,
      reimbursed: 3,
      pendingApprovals: 4,
      pendingReimbursements: 5,
    }
    vi.spyOn(dashboardApi, 'getDashboard').mockResolvedValue(dashboard)

    renderDashboard()

    expect(await screen.findByText('Pending Approvals')).toBeInTheDocument()
    expect(screen.getByText('Pending Reimbursements')).toBeInTheDocument()
  })

  // Scenario: Loading state renders while GET /api/dashboard is in flight
  it('renders a loading state while the request is in flight', () => {
    setUser('Employee')
    vi.spyOn(dashboardApi, 'getDashboard').mockReturnValue(new Promise(() => {}))

    renderDashboard()

    expect(screen.getByText('Loading…')).toBeInTheDocument()
  })

  // Scenario: Fetch error renders an error state, not zeroed/stale tiles
  it('renders an error state on fetch failure', async () => {
    setUser('Employee')
    vi.spyOn(dashboardApi, 'getDashboard').mockRejectedValue({
      code: 'INTERNAL_SERVER_ERROR',
      message: 'boom',
      fields: [],
      traceId: 't',
    })

    renderDashboard()

    expect(await screen.findByText('This dashboard could not be loaded.')).toBeInTheDocument()
    expect(screen.queryByText('Total Submitted')).not.toBeInTheDocument()
  })

  // Scenario: Compliance Officer sees no "Dashboard" nav link — covered at the AppLayout level,
  // not this page (AppLayout.test.tsx / manual verification); this page only owns the redirect.

  // Scenario: Compliance Officer navigating to /dashboard redirects to /expenses without calling GET /api/dashboard
  it('redirects ComplianceOfficer to /expenses without calling the dashboard endpoint', async () => {
    setUser('ComplianceOfficer')
    const spy = vi.spyOn(dashboardApi, 'getDashboard')

    renderDashboard()

    expect(await screen.findByText('expenses stub')).toBeInTheDocument()
    expect(spy).not.toHaveBeenCalled()
  })

  // Scenario: Employee/Manager/Finance navigating to /dashboard render normally, no redirect
  it.each(['Employee', 'Manager', 'Finance'] as const)('renders normally for %s, no redirect', async (role) => {
    setUser(role)
    vi.spyOn(dashboardApi, 'getDashboard').mockResolvedValue({ totalSubmitted: 0, approved: 0, reimbursed: 0 })

    renderDashboard()

    expect(await screen.findByText('Dashboard')).toBeInTheDocument()
    expect(screen.queryByText('expenses stub')).not.toBeInTheDocument()
  })
})
