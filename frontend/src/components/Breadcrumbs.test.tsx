import { beforeEach, describe, expect, it } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { Link, MemoryRouter, Outlet, Route, Routes, useParams } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import { useBreadcrumbStore } from '@/store/breadcrumbStore'
import { useBreadcrumb } from '@/store/useBreadcrumb'
import type { User } from '@/types/auth'
import { Breadcrumbs } from './Breadcrumbs'

function setUser(role: User['role']) {
  useAuthStore.setState({
    user: { id: 'u1', email: 'a@b.com', employeeNumber: 'EMP1', firstName: 'Ada', lastName: 'Lovelace', role },
    accessToken: 'token',
    status: 'authenticated',
  })
}

function Layout() {
  return (
    <div>
      <Breadcrumbs />
      <Outlet />
    </div>
  )
}

function DashboardStub() {
  useBreadcrumb('Dashboard')
  return <div>dashboard stub</div>
}

function ExpenseListStub() {
  useBreadcrumb('Expenses')
  return (
    <div>
      expenses stub
      <Link to="/expenses/exp-1">Open EXP-1</Link>
    </div>
  )
}

function FinanceSearchStub() {
  useBreadcrumb('Finance Search')
  return (
    <div>
      finance search stub
      <Link to="/expenses/exp-1">Open EXP-1</Link>
      <Link to="/expenses/exp-2">Open EXP-2</Link>
    </div>
  )
}

function ExpenseDetailStub() {
  const { id } = useParams<{ id: string }>()
  const label = id === 'exp-2' ? 'EXP-2' : 'EXP-1'
  useBreadcrumb(label)
  return <div>expense detail stub ({label})</div>
}

function renderApp(initialPath: string) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/dashboard" element={<DashboardStub />} />
          <Route path="/expenses" element={<ExpenseListStub />} />
          <Route path="/finance/search" element={<FinanceSearchStub />} />
          <Route path="/expenses/:id" element={<ExpenseDetailStub />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('Breadcrumbs', () => {
  beforeEach(() => {
    useBreadcrumbStore.setState({ trail: [] })
    setUser('Employee')
  })

  // Scenario: Reaching an expense detail from Finance Search shows the search in the trail
  it('shows Home > Finance Search > <Expense Number> when reached via Finance Search', () => {
    renderApp('/finance/search')

    fireEvent.click(screen.getByRole('link', { name: 'Open EXP-1' }))

    const nav = screen.getByRole('navigation')
    expect(nav).toHaveTextContent('Home')
    expect(nav).toHaveTextContent('Finance Search')
    expect(nav).toHaveTextContent('EXP-1')
  })

  // Scenario: Reaching the same expense detail from the expense list shows a different trail
  it('shows Home > Expenses > <Expense Number> when reached via the expense list', () => {
    renderApp('/expenses')

    fireEvent.click(screen.getByRole('link', { name: 'Open EXP-1' }))

    const nav = screen.getByRole('navigation')
    expect(nav).toHaveTextContent('Expenses')
    expect(nav).toHaveTextContent('EXP-1')
    expect(nav).not.toHaveTextContent('Finance Search')
  })

  // Scenario: A freshly loaded page with no prior navigation shows only Home and the current page
  it('shows only Home and the current page on a fresh load', () => {
    renderApp('/expenses')

    const nav = screen.getByRole('navigation')
    expect(nav).toHaveTextContent('Home')
    expect(nav).toHaveTextContent('Expenses')
    expect(nav).not.toHaveTextContent('Finance Search')
  })

  // Scenario: Expense detail page now offers backward navigation via the breadcrumb
  it('renders a clickable path back to the originating page on the detail screen', () => {
    renderApp('/expenses')
    fireEvent.click(screen.getByRole('link', { name: 'Open EXP-1' }))

    expect(screen.getByRole('button', { name: 'Expenses' })).toBeInTheDocument()
  })

  // Scenario: Home crumb links to the dashboard for non-Compliance roles
  it.each(['Employee', 'Manager', 'Finance'] as const)('links Home to /dashboard for %s', (role) => {
    setUser(role)
    renderApp('/expenses')

    expect(screen.getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/dashboard')
  })

  // Scenario: Home crumb links to the expense list for Compliance Officer
  it('links Home to /expenses for ComplianceOfficer', () => {
    setUser('ComplianceOfficer')
    renderApp('/expenses')

    expect(screen.getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/expenses')
  })

  // Scenario: Clicking an earlier crumb navigates to that page
  it('navigates to /finance/search when clicking the Finance Search crumb', () => {
    renderApp('/finance/search')
    fireEvent.click(screen.getByRole('link', { name: 'Open EXP-1' }))

    fireEvent.click(screen.getByRole('button', { name: 'Finance Search' }))

    expect(screen.getByText('finance search stub')).toBeInTheDocument()
  })

  // Scenario: Navigating via a breadcrumb truncates entries after it
  it('truncates the trail after navigating via a breadcrumb, building a fresh trail after', () => {
    renderApp('/finance/search')
    fireEvent.click(screen.getByRole('link', { name: 'Open EXP-1' }))
    fireEvent.click(screen.getByRole('button', { name: 'Finance Search' }))

    fireEvent.click(screen.getByRole('link', { name: 'Open EXP-2' }))

    const nav = screen.getByRole('navigation')
    expect(nav).toHaveTextContent('EXP-2')
    expect(nav).not.toHaveTextContent('EXP-1')
  })
})
