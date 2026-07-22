import { describe, expect, it } from 'vitest'
import { decodeJwtExpiry } from './jwt'

function base64url(value: object): string {
  return btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function makeToken(payload: object): string {
  const header = base64url({ alg: 'HS256', typ: 'JWT' })
  const body = base64url(payload)
  return `${header}.${body}.signature`
}

describe('decodeJwtExpiry', () => {
  it('decodes the exp claim to epoch milliseconds', () => {
    const exp = 1_700_000_000
    const token = makeToken({ sub: 'user-1', exp })

    expect(decodeJwtExpiry(token)).toBe(exp * 1000)
  })

  it('returns 0 for a token with no payload segment', () => {
    expect(decodeJwtExpiry('not-a-jwt')).toBe(0)
  })

  it('returns 0 when the exp claim is missing', () => {
    const token = makeToken({ sub: 'user-1' })

    expect(decodeJwtExpiry(token)).toBe(0)
  })

  it('returns 0 for an undecodable payload segment', () => {
    expect(decodeJwtExpiry('header.!!!not-base64!!!.signature')).toBe(0)
  })
})
