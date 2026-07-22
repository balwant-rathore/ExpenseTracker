import { beforeEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import type { User } from '@/types/auth'
import { RequireRole } from './RequireRole'

function financeUser(): User {
  return {
    id: 'u1',
    email: 'a@b.com',
    employeeNumber: 'EMP002',
    firstName: 'Grace',
    lastName: 'Hopper',
    role: 'Finance',
  }
}

function renderRestricted() {
  return render(
    <MemoryRouter initialEntries={['/finance-only']}>
      <Routes>
        <Route
          path="/finance-only"
          element={
            <RequireRole allowedRoles={['Finance']}>
              <div>finance content</div>
            </RequireRole>
          }
        />
        <Route path="/dashboard" element={<div>dashboard stub</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('RequireRole', () => {
  beforeEach(() => {
    useAuthStore.setState({ user: null, accessToken: null, status: 'unauthenticated' })
  })

  it('renders the content when the user has an allowed role', () => {
    useAuthStore.setState({ user: financeUser(), accessToken: 'token', status: 'authenticated' })

    renderRestricted()

    expect(screen.getByText('finance content')).toBeInTheDocument()
  })

  it('redirects away when the user has a disallowed role', () => {
    useAuthStore.setState({
      user: { ...financeUser(), role: 'Employee' },
      accessToken: 'token',
      status: 'authenticated',
    })

    renderRestricted()

    expect(screen.getByText('dashboard stub')).toBeInTheDocument()
    expect(screen.queryByText('finance content')).not.toBeInTheDocument()
  })

  it('reads only the store user role, never the access token contents', () => {
    // An access token that is not even a valid JWT — if RequireRole ever
    // tried to decode it, this would throw or behave unpredictably. It
    // must be entirely ignored; only user.role drives the decision.
    useAuthStore.setState({
      user: financeUser(),
      accessToken: 'not-a-real-jwt-at-all',
      status: 'authenticated',
    })

    renderRestricted()

    expect(screen.getByText('finance content')).toBeInTheDocument()
  })
})
