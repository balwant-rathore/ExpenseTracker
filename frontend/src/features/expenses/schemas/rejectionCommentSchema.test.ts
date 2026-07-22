import { describe, expect, it } from 'vitest'
import { rejectionCommentSchema } from './rejectionCommentSchema'

describe('rejectionCommentSchema', () => {
  it('accepts a valid trimmed comment', () => {
    const result = rejectionCommentSchema.safeParse({ rejectionComment: 'Missing receipt detail' })
    expect(result.success).toBe(true)
  })

  it('rejects an empty comment', () => {
    const result = rejectionCommentSchema.safeParse({ rejectionComment: '' })
    expect(result.success).toBe(false)
  })

  it('rejects a whitespace-only comment', () => {
    const result = rejectionCommentSchema.safeParse({ rejectionComment: '   ' })
    expect(result.success).toBe(false)
  })

  it('rejects a comment over 500 characters', () => {
    const result = rejectionCommentSchema.safeParse({ rejectionComment: 'x'.repeat(501) })
    expect(result.success).toBe(false)
  })

  it('accepts a comment at exactly 500 characters', () => {
    const result = rejectionCommentSchema.safeParse({ rejectionComment: 'x'.repeat(500) })
    expect(result.success).toBe(true)
  })
})
