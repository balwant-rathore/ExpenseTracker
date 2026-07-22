import { describe, expect, it } from 'vitest'
import { expenseFormSchema } from './expenseFormSchema'

const validBase = {
  expenseDate: '2020-01-01',
  category: 'Travel' as const,
  amount: '100',
  description: 'Taxi fare',
}

describe('expenseFormSchema', () => {
  it('accepts a fully valid payload', () => {
    expect(expenseFormSchema.safeParse(validBase).success).toBe(true)
  })

  it('rejects a non-positive amount', () => {
    const result = expenseFormSchema.safeParse({ ...validBase, amount: '0' })
    expect(result.success).toBe(false)
  })

  it('rejects a negative amount', () => {
    const result = expenseFormSchema.safeParse({ ...validBase, amount: '-5' })
    expect(result.success).toBe(false)
  })

  it('rejects a non-numeric amount', () => {
    const result = expenseFormSchema.safeParse({ ...validBase, amount: 'abc' })
    expect(result.success).toBe(false)
  })

  it('rejects an expense date in the future', () => {
    const future = new Date(Date.now() + 1000 * 60 * 60 * 24 * 30).toISOString().slice(0, 10)
    const result = expenseFormSchema.safeParse({ ...validBase, expenseDate: future })
    expect(result.success).toBe(false)
  })

  it('rejects a description over 500 characters', () => {
    const result = expenseFormSchema.safeParse({ ...validBase, description: 'x'.repeat(501) })
    expect(result.success).toBe(false)
  })

  it('accepts a description at exactly 500 characters', () => {
    const result = expenseFormSchema.safeParse({ ...validBase, description: 'x'.repeat(500) })
    expect(result.success).toBe(true)
  })

  it('rejects a category outside the seven defined values', () => {
    const result = expenseFormSchema.safeParse({ ...validBase, category: 'NotARealCategory' })
    expect(result.success).toBe(false)
  })
})
