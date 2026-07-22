import { apiRequest } from '@/lib/apiClient'
import type { DashboardResponse } from '../types/dashboard'

export function getDashboard(accessToken?: string | null): Promise<DashboardResponse> {
  return apiRequest<DashboardResponse>('/dashboard', { accessToken })
}
