import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom'
import { LoginPage } from '@/pages/LoginPage'
import { RegisterPage } from '@/pages/RegisterPage'
import { ForgotPasswordPage } from '@/pages/ForgotPasswordPage'
import { ResetPasswordPage } from '@/pages/ResetPasswordPage'
import { DashboardPage } from '@/pages/DashboardPage'
import { ExpenseListPage } from '@/pages/ExpenseListPage'
import { ExpenseDetailPage } from '@/pages/ExpenseDetailPage'
import { CreateExpensePage } from '@/pages/CreateExpensePage'
import { EditExpensePage } from '@/pages/EditExpensePage'
import { FinanceSearchPage } from '@/pages/FinanceSearchPage'
import { AppLayout } from '@/layouts/AppLayout'
import { ProtectedRoute } from './ProtectedRoute'
import { RequireRole } from './RequireRole'

const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/dashboard" replace /> },
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  { path: '/forgot-password', element: <ForgotPasswordPage /> },
  { path: '/reset-password', element: <ResetPasswordPage /> },
  {
    element: (
      <ProtectedRoute>
        <AppLayout />
      </ProtectedRoute>
    ),
    children: [
      { path: '/dashboard', element: <DashboardPage /> },
      { path: '/expenses', element: <ExpenseListPage /> },
      {
        path: '/expenses/new',
        element: (
          <RequireRole allowedRoles={['Employee', 'Manager']}>
            <CreateExpensePage />
          </RequireRole>
        ),
      },
      { path: '/expenses/:id', element: <ExpenseDetailPage /> },
      {
        path: '/expenses/:id/edit',
        element: (
          <RequireRole allowedRoles={['Employee', 'Manager']}>
            <EditExpensePage />
          </RequireRole>
        ),
      },
      {
        path: '/finance/search',
        element: (
          <RequireRole allowedRoles={['Finance']}>
            <FinanceSearchPage />
          </RequireRole>
        ),
      },
    ],
  },
])

export function AppRouter() {
  return <RouterProvider router={router} />
}
