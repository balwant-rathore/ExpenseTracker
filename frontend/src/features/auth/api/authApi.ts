import { apiRequest } from '@/lib/apiClient'
import type { User } from '@/types/auth'

export interface AuthResponse {
  user: User
  accessToken: string
  refreshToken: string
}

export interface RefreshResponse {
  accessToken: string
  refreshToken: string
}

export interface RegisterInput {
  employeeNumber: string
  email: string
  password: string
}

export interface LoginInput {
  email: string
  password: string
}

export interface ResetPasswordInput {
  email: string
  otp: string
  newPassword: string
}

export function register(input: RegisterInput): Promise<AuthResponse> {
  return apiRequest<AuthResponse>('/auth/register', { method: 'POST', body: input })
}

export function login(input: LoginInput): Promise<AuthResponse> {
  return apiRequest<AuthResponse>('/auth/login', { method: 'POST', body: input })
}

export function refresh(refreshToken: string): Promise<RefreshResponse> {
  return apiRequest<RefreshResponse>('/auth/refresh', { method: 'POST', body: { refreshToken } })
}

export function me(accessToken: string): Promise<User> {
  return apiRequest<User>('/auth/me', { accessToken })
}

export function logout(refreshToken: string, accessToken?: string | null): Promise<void> {
  return apiRequest<void>('/auth/logout', { method: 'POST', body: { refreshToken }, accessToken })
}

export function forgotPassword(email: string): Promise<void> {
  return apiRequest<void>('/auth/forgot-password', { method: 'POST', body: { email } })
}

export function resetPassword(input: ResetPasswordInput): Promise<void> {
  return apiRequest<void>('/auth/reset-password', { method: 'POST', body: input })
}
