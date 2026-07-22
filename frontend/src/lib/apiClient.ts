export interface ApiError {
  code: string
  message: string
  fields: string[]
  traceId: string
}

interface ApiRequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  accessToken?: string | null
}

const FALLBACK_ERROR: ApiError = {
  code: 'INTERNAL_SERVER_ERROR',
  message: 'An unexpected error occurred.',
  fields: [],
  traceId: '',
}

function isErrorEnvelope(value: unknown): value is { error: ApiError } {
  return (
    typeof value === 'object' &&
    value !== null &&
    'error' in value &&
    typeof (value as { error?: unknown }).error === 'object' &&
    (value as { error?: unknown }).error !== null
  )
}

export async function apiRequest<TResponse>(
  path: string,
  options: ApiRequestOptions = {},
): Promise<TResponse> {
  const { method = 'GET', body, accessToken } = options
  const isFormData = body instanceof FormData

  const headers: Record<string, string> = {}
  if (body !== undefined && !isFormData) {
    headers['Content-Type'] = 'application/json'
  }
  if (accessToken) {
    headers.Authorization = `Bearer ${accessToken}`
  }

  const response = await fetch(`/api${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : isFormData ? body : JSON.stringify(body),
  })

  if (response.status === 204) {
    return undefined as TResponse
  }

  const data: unknown = await response.json().catch(() => null)

  if (!response.ok) {
    throw isErrorEnvelope(data) ? data.error : FALLBACK_ERROR
  }

  return data as TResponse
}
