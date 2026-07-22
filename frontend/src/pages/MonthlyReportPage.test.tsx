import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import type { User } from '@/types/auth'
import { RequireRole } from '@/routes/RequireRole'
import * as reportsApi from '@/features/reports/api/reportsApi'
import * as blobDownload from '@/lib/blobDownload'
import { MonthlyReportPage } from './MonthlyReportPage'

function setUser(role: User['role']) {
  useAuthStore.setState({
    user: { id: 'u1', email: 'a@b.com', employeeNumber: 'EMP1', firstName: 'Ada', lastName: 'Lovelace', role },
    accessToken: 'token',
    status: 'authenticated',
  })
}

function renderGuardedPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/reports/monthly-reimbursement']}>
        <Routes>
          <Route
            path="/reports/monthly-reimbursement"
            element={
              <RequireRole allowedRoles={['Finance']}>
                <MonthlyReportPage />
              </RequireRole>
            }
          />
          <Route path="/dashboard" element={<div>dashboard stub</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function selectMonth(name: string) {
  fireEvent.click(screen.getByRole('combobox', { name: /month/i }))
  return screen.findByRole('option', { name }).then((option) => {
    fireEvent.pointerDown(option, { button: 0 })
    fireEvent.pointerUp(option, { button: 0 })
    fireEvent.click(option)
  })
}

describe('MonthlyReportPage', () => {
  const now = new Date()
  const currentYear = now.getFullYear()
  const currentMonth = now.getMonth() + 1

  beforeEach(() => {
    vi.restoreAllMocks()
    setUser('Finance')
  })

  // Scenario: Finance sees the report screen with current month/year pre-selected, no auto-download
  it('renders with the current month/year pre-selected and does not auto-download', () => {
    const spy = vi.spyOn(reportsApi, 'downloadMonthlyReimbursementReport')

    renderGuardedPage()

    expect(screen.getByRole('combobox', { name: /year/i })).toHaveTextContent(String(currentYear))
    expect(spy).not.toHaveBeenCalled()
  })

  // Scenario: Non-Finance role is redirected away from /reports/monthly-reimbursement
  it('redirects a non-Finance role away from the route', () => {
    setUser('Employee')

    renderGuardedPage()

    expect(screen.getByText('dashboard stub')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Download' })).not.toBeInTheDocument()
  })

  // Scenario: Clicking Download calls GET /api/reports/monthly-reimbursement with the selected year/month
  it('requests the selected period when Download is clicked', async () => {
    vi.spyOn(reportsApi, 'downloadMonthlyReimbursementReport').mockResolvedValue({
      blob: new Blob(['x']),
      fileName: 'Monthly-Reimbursement-2026-01.xlsx',
    })
    vi.spyOn(blobDownload, 'saveBlob').mockImplementation(() => {})

    renderGuardedPage()
    fireEvent.click(screen.getByRole('button', { name: 'Download' }))

    await waitFor(() =>
      expect(reportsApi.downloadMonthlyReimbursementReport).toHaveBeenCalledWith(currentYear, currentMonth, 'token'),
    )
  })

  // Scenario: Downloaded file is saved using the Content-Disposition file name, not a client-generated one
  it('saves the file using the server-provided file name', async () => {
    const blob = new Blob(['x'])
    vi.spyOn(reportsApi, 'downloadMonthlyReimbursementReport').mockResolvedValue({
      blob,
      fileName: 'Server-Named-Report.xlsx',
    })
    const saveSpy = vi.spyOn(blobDownload, 'saveBlob').mockImplementation(() => {})

    renderGuardedPage()
    fireEvent.click(screen.getByRole('button', { name: 'Download' }))

    await waitFor(() => expect(saveSpy).toHaveBeenCalledWith(blob, 'Server-Named-Report.xlsx'))
  })

  // Scenario: Changing the period, then downloading again, requests the newly selected period
  it('requests the newly selected period after changing month', async () => {
    vi.spyOn(reportsApi, 'downloadMonthlyReimbursementReport').mockResolvedValue({
      blob: new Blob(['x']),
      fileName: 'f.xlsx',
    })
    vi.spyOn(blobDownload, 'saveBlob').mockImplementation(() => {})

    renderGuardedPage()

    await selectMonth('March')
    await waitFor(() => expect(screen.getByRole('combobox', { name: /month/i })).toHaveTextContent('March'))

    fireEvent.click(screen.getByRole('button', { name: 'Download' }))

    await waitFor(() =>
      expect(reportsApi.downloadMonthlyReimbursementReport).toHaveBeenCalledWith(currentYear, 3, 'token'),
    )
  })

  // Scenario: A header-only (empty) workbook response is still saved without error
  it('saves an empty workbook response without error', async () => {
    const emptyBlob = new Blob([])
    vi.spyOn(reportsApi, 'downloadMonthlyReimbursementReport').mockResolvedValue({ blob: emptyBlob, fileName: null })
    const saveSpy = vi.spyOn(blobDownload, 'saveBlob').mockImplementation(() => {})

    renderGuardedPage()
    fireEvent.click(screen.getByRole('button', { name: 'Download' }))

    await waitFor(() =>
      expect(saveSpy).toHaveBeenCalledWith(
        emptyBlob,
        `Monthly-Reimbursement-${currentYear}-${String(currentMonth).padStart(2, '0')}.xlsx`,
      ),
    )
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  // Scenario: Backend 400 VALIDATION_ERROR is surfaced without attempting a file save
  it('surfaces a validation error without saving a file', async () => {
    vi.spyOn(reportsApi, 'downloadMonthlyReimbursementReport').mockRejectedValue({
      code: 'VALIDATION_ERROR',
      message: 'Year is required.',
      fields: ['year'],
      traceId: 't',
    })
    const saveSpy = vi.spyOn(blobDownload, 'saveBlob').mockImplementation(() => {})

    renderGuardedPage()
    fireEvent.click(screen.getByRole('button', { name: 'Download' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Year is required.')
    expect(saveSpy).not.toHaveBeenCalled()
  })
})
