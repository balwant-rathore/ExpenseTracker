import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { AttachmentPicker, type AttachmentSelection } from './AttachmentPicker'

function Harness({ initial }: { initial: AttachmentSelection }) {
  const [value, setValue] = useState<AttachmentSelection>(initial)
  return <AttachmentPicker value={value} onChange={setValue} />
}

function pdfFile(name = 'receipt.pdf', size = 1024) {
  return new File([new Uint8Array(size)], name, { type: 'application/pdf' })
}

describe('AttachmentPicker', () => {
  it('selecting a valid file holds it in state without calling any upload API', () => {
    const onChange = vi.fn()
    render(<AttachmentPicker value={{ kind: 'none' }} onChange={onChange} />)

    const input = screen.getByLabelText('Receipt attachment')
    const file = pdfFile()
    fireEvent.change(input, { target: { files: [file] } })

    expect(onChange).toHaveBeenCalledWith({ kind: 'new', file })
  })

  it('renders identically whether used from a create or edit context (same component, no API concerns)', () => {
    const { rerender } = render(<AttachmentPicker value={{ kind: 'none' }} onChange={vi.fn()} />)
    expect(screen.getByRole('button', { name: /select receipt/i })).toBeInTheDocument()

    rerender(
      <AttachmentPicker
        value={{ kind: 'existing', attachmentId: 'a1', fileName: 'old-receipt.pdf' }}
        onChange={vi.fn()}
      />,
    )
    expect(screen.getByRole('button', { name: /replace receipt/i })).toBeInTheDocument()
    expect(screen.getByText('old-receipt.pdf')).toBeInTheDocument()
  })

  it('rejects a disallowed file type client-side and does not update the selection', () => {
    render(<Harness initial={{ kind: 'none' }} />)

    const input = screen.getByLabelText('Receipt attachment')
    fireEvent.change(input, { target: { files: [new File(['x'], 'receipt.docx', { type: 'application/msword' })] } })

    expect(screen.getByRole('alert')).toHaveTextContent('File type must be PDF, JPG, or PNG.')
    expect(screen.queryByText('receipt.docx')).not.toBeInTheDocument()
  })

  it('accepts an allowed file type client-side with no error shown', () => {
    render(<Harness initial={{ kind: 'none' }} />)

    const input = screen.getByLabelText('Receipt attachment')
    fireEvent.change(input, { target: { files: [pdfFile()] } })

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByText('receipt.pdf')).toBeInTheDocument()
  })

  it('rejects an oversized file client-side', () => {
    render(<Harness initial={{ kind: 'none' }} />)

    const input = screen.getByLabelText('Receipt attachment')
    const oversized = pdfFile('big.pdf', 10 * 1024 * 1024 + 1)
    fireEvent.change(input, { target: { files: [oversized] } })

    expect(screen.getByRole('alert')).toHaveTextContent('File size must not exceed 10 MB.')
  })

  it('accepts a file at exactly the 10 MB boundary', () => {
    render(<Harness initial={{ kind: 'none' }} />)

    const input = screen.getByLabelText('Receipt attachment')
    const atLimit = pdfFile('at-limit.pdf', 10 * 1024 * 1024)
    fireEvent.change(input, { target: { files: [atLimit] } })

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByText('at-limit.pdf')).toBeInTheDocument()
  })

  it('shows the existing attachment file name by default in an edit context', () => {
    render(
      <AttachmentPicker
        value={{ kind: 'existing', attachmentId: 'a1', fileName: 'my-receipt.pdf' }}
        onChange={vi.fn()}
      />,
    )

    expect(screen.getByText('my-receipt.pdf')).toBeInTheDocument()
  })
})
