import { decodeJwtExpiry } from '@/lib/jwt'

const REFRESH_MARGIN_MS = 60_000

/**
 * Milliseconds until the access token should be proactively refreshed —
 * its expiry minus a safety margin for clock skew/latency. Clamped to 0 so
 * an already-expired (or undecodable, per decodeJwtExpiry's fallback) token
 * schedules an immediate refresh rather than a negative delay.
 */
export function computeRefreshDelayMs(accessToken: string, now: number = Date.now()): number {
  const expiryMs = decodeJwtExpiry(accessToken)
  return Math.max(0, expiryMs - now - REFRESH_MARGIN_MS)
}

/**
 * Arms a timer to call `onRefreshDue` shortly before `accessToken` expires.
 * Returns a cancel function to clear the timer (e.g. on unmount or when the
 * token is replaced by a newer one).
 */
export function scheduleProactiveRefresh(accessToken: string, onRefreshDue: () => void): () => void {
  const delay = computeRefreshDelayMs(accessToken)
  const timeoutId = setTimeout(onRefreshDue, delay)
  return () => clearTimeout(timeoutId)
}
