import { apiRequestBlob, type BlobResponse } from '@/lib/apiClient'

export function downloadMonthlyReimbursementReport(
  year: number,
  month: number,
  accessToken?: string | null,
): Promise<BlobResponse> {
  return apiRequestBlob(`/reports/monthly-reimbursement?year=${year}&month=${month}`, { accessToken })
}
