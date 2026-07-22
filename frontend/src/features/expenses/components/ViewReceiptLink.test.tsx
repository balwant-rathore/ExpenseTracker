import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '@/test/renderWithProviders'
import { useAuthStore } from '@/store/authStore'
import type { User } from '@/types/auth'
import * as attachmentApi from '../api/attachmentApi'
import * as blobDownload from '@/lib/blobDownload'
import { ViewReceiptLink } from './ViewReceiptLink'

function setUser(role: User['role']) {
  useAuthStore.setState({
    user: { id: 'u1', email: 'a@b.com', employeeNumber: 'EMP1', firstName: 'Ada', lastName: 'Lovelace', role },
    accessToken: 'token',
    status: 'authenticated',
  })
}

describe('ViewReceiptLink', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    setUser('Employee')
  })

  // Scenario: Detail screen shows a View Receipt link labeled with attachmentOriginalFileName
  it('renders a link labeled with the attachment file name', () => {
    renderWithProviders(<ViewReceiptLink attachmentId="attachment-1" fileName="receipt.pdf" />)

    expect(screen.getByRole('button', { name: /receipt\.pdf/i })).toBeInTheDocument()
  })

  // Scenario: Clicking View Receipt fetches the file with authentication and opens a new tab
  it('fetches with the caller access token and opens a new tab on success', async () => {
    const blob = new Blob(['x'])
    vi.spyOn(attachmentApi, 'viewAttachment').mockResolvedValue({ blob, fileName: 'receipt.pdf' })
    const openSpy = vi.spyOn(blobDownload, 'openBlobInNewTab').mockReturnValue({} as Window)

    renderWithProviders(<ViewReceiptLink attachmentId="attachment-1" fileName="receipt.pdf" />)
    fireEvent.click(screen.getByRole('button', { name: /receipt\.pdf/i }))

    await waitFor(() => expect(attachmentApi.viewAttachment).toHaveBeenCalledWith('attachment-1', 'token'))
    await waitFor(() => expect(openSpy).toHaveBeenCalledWith(blob))
  })

  // Scenario: Backend rejection is surfaced without opening a broken tab
  it.each([
    { code: 'RESOURCE_NOT_FOUND', message: 'Attachment not found.' },
    { code: 'AUTHORIZATION_FAILED', message: 'You do not have permission to perform this action.' },
  ])('surfaces a $code error without opening a tab', async ({ code, message }) => {
    vi.spyOn(attachmentApi, 'viewAttachment').mockRejectedValue({ code, message, fields: [], traceId: 't' })
    const openSpy = vi.spyOn(blobDownload, 'openBlobInNewTab')

    renderWithProviders(<ViewReceiptLink attachmentId="attachment-1" fileName="receipt.pdf" />)
    fireEvent.click(screen.getByRole('button', { name: /receipt\.pdf/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(message)
    expect(openSpy).not.toHaveBeenCalled()
  })

  // Scenario: Any viewer who can see the expense detail also sees the receipt link
  it.each(['Employee', 'Manager', 'Finance', 'ComplianceOfficer'] as const)(
    'renders and works the same for %s viewers (no additional role gate)',
    async (role) => {
      setUser(role)
      const blob = new Blob(['x'])
      vi.spyOn(attachmentApi, 'viewAttachment').mockResolvedValue({ blob, fileName: 'receipt.pdf' })
      vi.spyOn(blobDownload, 'openBlobInNewTab').mockReturnValue({} as Window)

      renderWithProviders(<ViewReceiptLink attachmentId="attachment-1" fileName="receipt.pdf" />)

      const link = screen.getByRole('button', { name: /receipt\.pdf/i })
      expect(link).toBeInTheDocument()
      fireEvent.click(link)

      await waitFor(() => expect(attachmentApi.viewAttachment).toHaveBeenCalledWith('attachment-1', 'token'))
    },
  )
})
