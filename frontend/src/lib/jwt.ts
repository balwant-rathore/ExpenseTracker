/**
 * Decodes a JWT's `exp` claim without verifying the signature — this is a
 * client-side scheduling hint only; the backend remains the source of truth
 * for whether the token is actually still valid.
 */
export function decodeJwtExpiry(token: string): number {
  try {
    const payloadSegment = token.split('.')[1]
    if (!payloadSegment) {
      return 0
    }

    const base64 = payloadSegment.replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
    const payload: unknown = JSON.parse(atob(padded))

    if (
      typeof payload !== 'object' ||
      payload === null ||
      typeof (payload as { exp?: unknown }).exp !== 'number'
    ) {
      return 0
    }

    return (payload as { exp: number }).exp * 1000
  } catch {
    return 0
  }
}
