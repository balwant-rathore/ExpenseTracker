import { beforeEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import type { User } from '@/types/auth'
import { AppLayout } from './AppLayout'

function setUser(role: User['role']) {
  useAuthStore.setState({
    user: { id: 'u1', email: 'a@b.com', employeeNumber: 'EMP1', firstName: 'Ada', lastName: 'Lovelace', role },
    accessToken: 'token',
    status: 'authenticated',
  })
}

function renderLayout() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/dashboard']}>
        <Routes>
          <Route element={<AppLayout />}>
            <Route path="/dashboard" element={<div>dashboard content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('AppLayout', () => {
  beforeEach(() => {
    useAuthStore.setState({ user: null, accessToken: null, status: 'unauthenticated' })
  })

  // Scenario: Compliance Officer sees no "Dashboard" nav link
  it('hides the Dashboard nav link for ComplianceOfficer, shows My Expenses instead', () => {
    setUser('ComplianceOfficer')

    renderLayout()

    expect(screen.queryByRole('link', { name: 'Dashboard' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'My Expenses' })).toBeInTheDocument()
  })

  it('shows the Dashboard nav link for non-Compliance roles', () => {
    setUser('Employee')

    renderLayout()

    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeInTheDocument()
  })
})
