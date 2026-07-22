import { beforeEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import { ProtectedRoute } from './ProtectedRoute'

function renderProtected() {
  return render(
    <MemoryRouter initialEntries={['/dashboard']}>
      <Routes>
        <Route
          path="/dashboard"
          element={
            <ProtectedRoute>
              <div>protected content</div>
            </ProtectedRoute>
          }
        />
        <Route path="/login" element={<div>login page stub</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('ProtectedRoute', () => {
  beforeEach(() => {
    useAuthStore.setState({ user: null, accessToken: null, status: 'bootstrapping' })
  })

  it('redirects to /login without rendering content when unauthenticated', () => {
    useAuthStore.setState({ status: 'unauthenticated' })

    renderProtected()

    expect(screen.getByText('login page stub')).toBeInTheDocument()
    expect(screen.queryByText('protected content')).not.toBeInTheDocument()
  })

  it('renders the protected content when authenticated', () => {
    useAuthStore.setState({
      status: 'authenticated',
      user: {
        id: 'u1',
        email: 'a@b.com',
        employeeNumber: 'EMP001',
        firstName: 'Ada',
        lastName: 'Lovelace',
        role: 'Employee',
      },
      accessToken: 'token',
    })

    renderProtected()

    expect(screen.getByText('protected content')).toBeInTheDocument()
  })

  it('shows a loading state instead of redirecting while bootstrapping', () => {
    useAuthStore.setState({ status: 'bootstrapping' })

    renderProtected()

    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.queryByText('login page stub')).not.toBeInTheDocument()
    expect(screen.queryByText('protected content')).not.toBeInTheDocument()
  })
})
