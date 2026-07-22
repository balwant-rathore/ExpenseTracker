import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { computeRefreshDelayMs, scheduleProactiveRefresh } from './scheduleProactiveRefresh'

function base64url(value: object): string {
  return btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function makeToken(exp: number): string {
  return `${base64url({ alg: 'HS256' })}.${base64url({ sub: 'user-1', exp })}.signature`
}

describe('computeRefreshDelayMs', () => {
  it('returns exp minus now minus the 60s safety margin', () => {
    const now = 1_000_000_000_000
    const expSeconds = now / 1000 + 15 * 60
    const token = makeToken(expSeconds)

    expect(computeRefreshDelayMs(token, now)).toBe(15 * 60 * 1000 - 60_000)
  })

  it('clamps to 0 when the token has already expired', () => {
    const now = 1_000_000_000_000
    const expSeconds = now / 1000 - 60
    const token = makeToken(expSeconds)

    expect(computeRefreshDelayMs(token, now)).toBe(0)
  })

  it('clamps to 0 for an undecodable token', () => {
    expect(computeRefreshDelayMs('not-a-jwt', 1_000_000_000_000)).toBe(0)
  })
})

describe('scheduleProactiveRefresh', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('calls onRefreshDue after the computed delay', () => {
    const now = Date.now()
    const token = makeToken(now / 1000 + 120) // expires in 2 min -> 1 min delay after margin
    const onRefreshDue = vi.fn()

    scheduleProactiveRefresh(token, onRefreshDue)

    vi.advanceTimersByTime(60_000 - 1)
    expect(onRefreshDue).not.toHaveBeenCalled()

    vi.advanceTimersByTime(1)
    expect(onRefreshDue).toHaveBeenCalledTimes(1)
  })

  it('the returned cancel function prevents onRefreshDue from firing', () => {
    const now = Date.now()
    const token = makeToken(now / 1000 + 120)
    const onRefreshDue = vi.fn()

    const cancel = scheduleProactiveRefresh(token, onRefreshDue)
    cancel()

    vi.advanceTimersByTime(60_000)
    expect(onRefreshDue).not.toHaveBeenCalled()
  })
})
