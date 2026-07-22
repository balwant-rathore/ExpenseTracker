import { describe, expect, it, vi, beforeEach } from 'vitest'
import { apiRequest, type ApiError } from './apiClient'

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('apiRequest', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('sends a GET request to the /api-prefixed path and returns the parsed body', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, { hello: 'world' }))

    const result = await apiRequest<{ hello: string }>('/dashboard')

    expect(fetch).toHaveBeenCalledWith(
      '/api/dashboard',
      expect.objectContaining({ method: 'GET' }),
    )
    expect(result).toEqual({ hello: 'world' })
  })

  it('sends a POST body as JSON with a Content-Type header', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, {}))

    await apiRequest('/auth/login', { method: 'POST', body: { email: 'a@b.com', password: 'x' } })

    const init = vi.mocked(fetch).mock.calls[0][1]
    expect(init).toBeDefined()
    expect(init!.method).toBe('POST')
    expect(init!.body).toBe(JSON.stringify({ email: 'a@b.com', password: 'x' }))
    expect((init!.headers as Record<string, string>)['Content-Type']).toBe('application/json')
  })

  it('passes a FormData body through unchanged without JSON.stringify or a Content-Type header', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(201, { attachmentId: 'abc-123' }))

    const formData = new FormData()
    formData.append('file', new File(['data'], 'receipt.pdf', { type: 'application/pdf' }))

    await apiRequest('/attachments', { method: 'POST', body: formData })

    const init = vi.mocked(fetch).mock.calls[0][1]
    expect(init).toBeDefined()
    expect(init!.body).toBe(formData)
    expect((init!.headers as Record<string, string>)['Content-Type']).toBeUndefined()
  })

  it('attaches an Authorization header when accessToken is provided', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 204 }))

    await apiRequest('/auth/logout', { method: 'POST', accessToken: 'token-123' })

    const init = vi.mocked(fetch).mock.calls[0][1]
    expect(init).toBeDefined()
    expect((init!.headers as Record<string, string>).Authorization).toBe('Bearer token-123')
  })

  it('omits the Authorization header when no accessToken is provided', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, {}))

    await apiRequest('/auth/login', { method: 'POST', body: {} })

    const init = vi.mocked(fetch).mock.calls[0][1]
    expect(init).toBeDefined()
    expect((init!.headers as Record<string, string>).Authorization).toBeUndefined()
  })

  it('returns undefined for a 204 No Content response without parsing a body', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 204 }))

    const result = await apiRequest('/auth/logout', { method: 'POST' })

    expect(result).toBeUndefined()
  })

  it('throws the parsed ApiError for a non-2xx response with an error envelope', async () => {
    const apiError: ApiError = {
      code: 'AUTHENTICATION_FAILED',
      message: 'Invalid email or password.',
      fields: [],
      traceId: 'trace-1',
    }
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(401, { error: apiError }))

    await expect(apiRequest('/auth/login', { method: 'POST', body: {} })).rejects.toEqual(
      apiError,
    )
  })

  it('throws a fallback error for a non-2xx response with an unparseable body', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response('not json', { status: 500 }))

    await expect(apiRequest('/auth/login', { method: 'POST', body: {} })).rejects.toEqual({
      code: 'INTERNAL_SERVER_ERROR',
      message: 'An unexpected error occurred.',
      fields: [],
      traceId: '',
    })
  })
})
