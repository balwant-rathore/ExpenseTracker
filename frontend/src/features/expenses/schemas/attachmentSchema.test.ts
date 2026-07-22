import { describe, expect, it } from 'vitest'
import { validateAttachmentFile } from './attachmentSchema'

function makeFile(name: string, sizeBytes: number, type: string): File {
  const buffer = new Uint8Array(sizeBytes)
  return new File([buffer], name, { type })
}

describe('validateAttachmentFile', () => {
  it('accepts a PDF file at or under 10 MB', () => {
    const file = makeFile('receipt.pdf', 1024, 'application/pdf')
    expect(validateAttachmentFile(file)).toBeNull()
  })

  it('accepts a file at exactly the 10 MB boundary', () => {
    const file = makeFile('receipt.jpg', 10 * 1024 * 1024, 'image/jpeg')
    expect(validateAttachmentFile(file)).toBeNull()
  })

  it('rejects a disallowed file type', () => {
    const file = makeFile('receipt.docx', 1024, 'application/msword')
    expect(validateAttachmentFile(file)).toBe('File type must be PDF, JPG, or PNG.')
  })

  it('rejects a file over 10 MB', () => {
    const file = makeFile('receipt.png', 10 * 1024 * 1024 + 1, 'image/png')
    expect(validateAttachmentFile(file)).toBe('File size must not exceed 10 MB.')
  })
})
