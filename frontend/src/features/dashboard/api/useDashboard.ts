import { useQuery } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import { useAuthStore } from '@/store/authStore'
import type { DashboardResponse } from '../types/dashboard'
import { getDashboard } from './dashboardApi'

export function useDashboard(enabled = true) {
  const accessToken = useAuthStore((state) => state.accessToken)

  return useQuery<DashboardResponse, ApiError>({
    queryKey: ['dashboard'],
    queryFn: () => getDashboard(accessToken),
    enabled,
  })
}
