import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '@/test/renderWithProviders'
import { useAuthStore } from '@/store/authStore'
import type { ExpenseResponse } from '@/types/expense'
import * as expenseApi from '../api/expenseApi'
import * as attachmentApi from '../api/attachmentApi'
import { ExpenseForm } from './ExpenseForm'

const pastIso = '2020-01-01'

function pdfFile(name = 'receipt.pdf') {
  return new File([new Uint8Array(1024)], name, { type: 'application/pdf' })
}

async function selectCategory(label: string) {
  fireEvent.click(screen.getByLabelText('Category'))
  fireEvent.click(await screen.findByRole('option', { name: label }))
}

async function fillCommonFields() {
  fireEvent.change(screen.getByLabelText('Expense date'), { target: { value: pastIso } })
  await selectCategory('Travel')
  fireEvent.change(screen.getByLabelText('Amount'), { target: { value: '100' } })
  fireEvent.change(screen.getByLabelText('Description'), { target: { value: 'Taxi fare' } })
}

const baseExpense: ExpenseResponse = {
  id: 'expense-1',
  expenseNumber: 'EXP-20260101-0001',
  expenseDate: pastIso,
  category: 'Travel',
  amount: 100,
  currency: 'INR',
  description: 'Original description',
  status: 'Draft',
  submittedAt: null,
  approvedAt: null,
  complianceApprovedAt: null,
  rejectedAt: null,
  rejectionComment: null,
  reimbursedAt: null,
  createdAt: '2026-01-01T00:00:00Z',
  employeeName: 'Ada Lovelace',
  employeeNumber: 'EMP001',
  receiptAttachmentId: 'attachment-1',
  attachmentOriginalFileName: 'original-receipt.pdf',
}

describe('ExpenseForm', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    useAuthStore.setState({
      user: {
        id: 'employee-1',
        email: 'a@b.com',
        employeeNumber: 'EMP1',
        firstName: 'Ada',
        lastName: 'Lovelace',
        role: 'Employee',
      },
      accessToken: 'token',
      status: 'authenticated',
    })
  })

  describe('create mode', () => {
    it('does not render an editable currency field (fixed to INR)', () => {
      renderWithProviders(<ExpenseForm mode="create" />)
      const currencyInput = screen.getByLabelText('Currency') as HTMLInputElement
      expect(currencyInput.value).toBe('INR')
      expect(currencyInput).toBeDisabled()
    })

    it('calls createExpense with action Draft and the chosen action on Save as Draft', async () => {
      const createSpy = vi.spyOn(expenseApi, 'createExpense').mockResolvedValue({
        expense: { ...baseExpense, status: 'Draft' },
      })
      vi.spyOn(attachmentApi, 'uploadAttachment').mockResolvedValue({ attachmentId: 'new-attachment' })

      renderWithProviders(<ExpenseForm mode="create" />)
      await fillCommonFields()
      fireEvent.change(screen.getByLabelText('Receipt attachment'), { target: { files: [pdfFile()] } })
      fireEvent.click(screen.getByRole('button', { name: /save as draft/i }))

      await waitFor(() =>
        expect(createSpy).toHaveBeenCalledWith(
          expect.objectContaining({ action: 'Draft', receiptAttachmentId: 'new-attachment' }),
          'token',
        ),
      )
    })

    it('calls createExpense with action Submit on the Submit button', async () => {
      const createSpy = vi.spyOn(expenseApi, 'createExpense').mockResolvedValue({
        expense: { ...baseExpense, status: 'Submitted' },
      })
      vi.spyOn(attachmentApi, 'uploadAttachment').mockResolvedValue({ attachmentId: 'new-attachment' })

      renderWithProviders(<ExpenseForm mode="create" />)
      await fillCommonFields()
      fireEvent.change(screen.getByLabelText('Receipt attachment'), { target: { files: [pdfFile()] } })
      fireEvent.click(screen.getByRole('button', { name: /^submit$/i }))

      await waitFor(() =>
        expect(createSpy).toHaveBeenCalledWith(expect.objectContaining({ action: 'Submit' }), 'token'),
      )
    })

    it('blocks submission and makes no API call when amount is non-positive, for both Draft and Submit', async () => {
      const createSpy = vi.spyOn(expenseApi, 'createExpense')

      renderWithProviders(<ExpenseForm mode="create" />)
      await fillCommonFields()
      fireEvent.change(screen.getByLabelText('Amount'), { target: { value: '0' } })
      fireEvent.change(screen.getByLabelText('Receipt attachment'), { target: { files: [pdfFile()] } })

      fireEvent.click(screen.getByRole('button', { name: /save as draft/i }))
      expect(await screen.findByText('Amount must be greater than zero.')).toBeInTheDocument()
      expect(createSpy).not.toHaveBeenCalled()

      fireEvent.click(screen.getByRole('button', { name: /^submit$/i }))
      expect(await screen.findByText('Amount must be greater than zero.')).toBeInTheDocument()
      expect(createSpy).not.toHaveBeenCalled()
    })

    it('blocks submission when expense date is in the future', async () => {
      const createSpy = vi.spyOn(expenseApi, 'createExpense')
      const futureDate = new Date(Date.now() + 1000 * 60 * 60 * 24 * 10).toISOString().slice(0, 10)

      renderWithProviders(<ExpenseForm mode="create" />)
      await fillCommonFields()
      fireEvent.change(screen.getByLabelText('Expense date'), { target: { value: futureDate } })
      fireEvent.change(screen.getByLabelText('Receipt attachment'), { target: { files: [pdfFile()] } })
      fireEvent.click(screen.getByRole('button', { name: /save as draft/i }))

      expect(await screen.findByText('Expense date cannot be in the future.')).toBeInTheDocument()
      expect(createSpy).not.toHaveBeenCalled()
    })

    it('blocks submission when description exceeds 500 characters', async () => {
      const createSpy = vi.spyOn(expenseApi, 'createExpense')

      renderWithProviders(<ExpenseForm mode="create" />)
      await fillCommonFields()
      fireEvent.change(screen.getByLabelText('Description'), { target: { value: 'x'.repeat(501) } })
      fireEvent.change(screen.getByLabelText('Receipt attachment'), { target: { files: [pdfFile()] } })
      fireEvent.click(screen.getByRole('button', { name: /save as draft/i }))

      expect(
        await screen.findByText('Description must not exceed 500 characters.'),
      ).toBeInTheDocument()
      expect(createSpy).not.toHaveBeenCalled()
    })

    it('blocks submission with no attachment selected', async () => {
      const createSpy = vi.spyOn(expenseApi, 'createExpense')

      renderWithProviders(<ExpenseForm mode="create" />)
      await fillCommonFields()
      fireEvent.click(screen.getByRole('button', { name: /save as draft/i }))

      expect(await screen.findByText('A receipt attachment is required.')).toBeInTheDocument()
      expect(createSpy).not.toHaveBeenCalled()
    })

    it('blocks expense creation when the deferred attachment upload fails', async () => {
      vi.spyOn(attachmentApi, 'uploadAttachment').mockRejectedValue({
        code: 'VALIDATION_ERROR',
        message: 'File type must be PDF, JPG, or PNG.',
        fields: ['file'],
        traceId: 't',
      })
      const createSpy = vi.spyOn(expenseApi, 'createExpense')

      renderWithProviders(<ExpenseForm mode="create" />)
      await fillCommonFields()
      fireEvent.change(screen.getByLabelText('Receipt attachment'), { target: { files: [pdfFile()] } })
      fireEvent.click(screen.getByRole('button', { name: /save as draft/i }))

      expect(
        await screen.findByText('Failed to upload the receipt. Please try again.'),
      ).toBeInTheDocument()
      expect(createSpy).not.toHaveBeenCalled()
    })

    it('surfaces a backend rejection even when client-side validation passed', async () => {
      vi.spyOn(attachmentApi, 'uploadAttachment').mockResolvedValue({ attachmentId: 'new-attachment' })
      vi.spyOn(expenseApi, 'createExpense').mockRejectedValue({
        code: 'BUSINESS_RULE_VIOLATION',
        message: 'The referenced attachment could not be found.',
        fields: [],
        traceId: 't',
      })

      renderWithProviders(<ExpenseForm mode="create" />)
      await fillCommonFields()
      fireEvent.change(screen.getByLabelText('Receipt attachment'), { target: { files: [pdfFile()] } })
      fireEvent.click(screen.getByRole('button', { name: /save as draft/i }))

      const alert = await screen.findByRole('alert')
      expect(alert).toHaveTextContent('The referenced attachment could not be found.')
    })
  })

  describe('edit mode', () => {
    it('pre-fills the form with the expense current values', () => {
      renderWithProviders(<ExpenseForm mode="edit" expense={baseExpense} />)

      expect((screen.getByLabelText('Expense date') as HTMLInputElement).value).toBe(pastIso)
      expect((screen.getByLabelText('Amount') as HTMLInputElement).value).toBe('100')
      expect((screen.getByLabelText('Description') as HTMLTextAreaElement).value).toBe(
        'Original description',
      )
    })

    it('has no status control anywhere in the form', () => {
      renderWithProviders(<ExpenseForm mode="edit" expense={baseExpense} />)
      expect(screen.queryByLabelText(/status/i)).not.toBeInTheDocument()
    })

    it('shows the existing attachment file name by default', () => {
      renderWithProviders(<ExpenseForm mode="edit" expense={baseExpense} />)
      expect(screen.getByText('original-receipt.pdf')).toBeInTheDocument()
    })

    it('calls updateExpense with edited values on save', async () => {
      const updateSpy = vi
        .spyOn(expenseApi, 'updateExpense')
        .mockResolvedValue({ expense: { ...baseExpense, description: 'Updated' } })

      renderWithProviders(<ExpenseForm mode="edit" expense={baseExpense} />)
      fireEvent.change(screen.getByLabelText('Description'), { target: { value: 'Updated' } })
      fireEvent.click(screen.getByRole('button', { name: /save changes/i }))

      await waitFor(() =>
        expect(updateSpy).toHaveBeenCalledWith(
          'expense-1',
          expect.objectContaining({ description: 'Updated', receiptAttachmentId: 'attachment-1' }),
          'token',
        ),
      )
    })

    it('resubmits the same attachment id when the attachment is left unchanged, with no upload call', async () => {
      const updateSpy = vi
        .spyOn(expenseApi, 'updateExpense')
        .mockResolvedValue({ expense: baseExpense })
      const uploadSpy = vi.spyOn(attachmentApi, 'uploadAttachment')

      renderWithProviders(<ExpenseForm mode="edit" expense={baseExpense} />)
      fireEvent.click(screen.getByRole('button', { name: /save changes/i }))

      await waitFor(() =>
        expect(updateSpy).toHaveBeenCalledWith(
          'expense-1',
          expect.objectContaining({ receiptAttachmentId: 'attachment-1' }),
          'token',
        ),
      )
      expect(uploadSpy).not.toHaveBeenCalled()
    })

    it('uses the newly uploaded attachment id when the attachment is replaced', async () => {
      vi.spyOn(attachmentApi, 'uploadAttachment').mockResolvedValue({ attachmentId: 'new-attachment' })
      const updateSpy = vi
        .spyOn(expenseApi, 'updateExpense')
        .mockResolvedValue({ expense: baseExpense })

      renderWithProviders(<ExpenseForm mode="edit" expense={baseExpense} />)
      fireEvent.change(screen.getByLabelText('Receipt attachment'), {
        target: { files: [pdfFile('new-receipt.pdf')] },
      })
      fireEvent.click(screen.getByRole('button', { name: /save changes/i }))

      await waitFor(() =>
        expect(updateSpy).toHaveBeenCalledWith(
          'expense-1',
          expect.objectContaining({ receiptAttachmentId: 'new-attachment' }),
          'token',
        ),
      )
    })

    it('surfaces a backend rejection on a stale edit attempt', async () => {
      vi.spyOn(expenseApi, 'updateExpense').mockRejectedValue({
        code: 'BUSINESS_RULE_VIOLATION',
        message: 'Only expenses in Draft or Submitted status can be edited.',
        fields: [],
        traceId: 't',
      })

      renderWithProviders(<ExpenseForm mode="edit" expense={baseExpense} />)
      fireEvent.click(screen.getByRole('button', { name: /save changes/i }))

      const alert = await screen.findByRole('alert')
      expect(alert).toHaveTextContent('Only expenses in Draft or Submitted status can be edited.')
    })

    it('blocks the edit save when the deferred attachment upload fails', async () => {
      vi.spyOn(attachmentApi, 'uploadAttachment').mockRejectedValue({
        code: 'VALIDATION_ERROR',
        message: 'File size must be between 1 byte and 10 MB.',
        fields: ['file'],
        traceId: 't',
      })
      const updateSpy = vi.spyOn(expenseApi, 'updateExpense')

      renderWithProviders(<ExpenseForm mode="edit" expense={baseExpense} />)
      fireEvent.change(screen.getByLabelText('Receipt attachment'), {
        target: { files: [pdfFile('new-receipt.pdf')] },
      })
      fireEvent.click(screen.getByRole('button', { name: /save changes/i }))

      expect(
        await screen.findByText('Failed to upload the receipt. Please try again.'),
      ).toBeInTheDocument()
      expect(updateSpy).not.toHaveBeenCalled()
    })
  })
})
