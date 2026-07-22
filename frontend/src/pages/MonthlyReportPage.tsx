import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { saveBlob } from '@/lib/blobDownload'
import type { ApiError } from '@/lib/apiClient'
import { useAuthStore } from '@/store/authStore'
import { useBreadcrumb } from '@/store/useBreadcrumb'
import { downloadMonthlyReimbursementReport } from '@/features/reports/api/reportsApi'
import { MonthYearPicker } from '@/features/reports/components/MonthYearPicker'

const now = new Date()
const CURRENT_YEAR = now.getFullYear()
const CURRENT_MONTH = now.getMonth() + 1
const YEAR_OPTIONS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - i)

function fallbackFileName(year: number, month: number): string {
  return `Monthly-Reimbursement-${year}-${String(month).padStart(2, '0')}.xlsx`
}

export function MonthlyReportPage() {
  useBreadcrumb('Monthly Report')
  const [year, setYear] = useState(CURRENT_YEAR)
  const [month, setMonth] = useState(CURRENT_MONTH)
  const accessToken = useAuthStore((state) => state.accessToken)

  const downloadMutation = useMutation<void, ApiError, void>({
    mutationFn: async () => {
      const { blob, fileName } = await downloadMonthlyReimbursementReport(year, month, accessToken)
      saveBlob(blob, fileName ?? fallbackFileName(year, month))
    },
  })

  return (
    <div className="flex flex-col gap-4 p-4">
      <h1 className="text-xl font-medium">Monthly Reimbursement Report</h1>

      <MonthYearPicker year={year} month={month} years={YEAR_OPTIONS} onYearChange={setYear} onMonthChange={setMonth} />

      <div>
        <Button onClick={() => downloadMutation.mutate()} disabled={downloadMutation.isPending}>
          {downloadMutation.isPending ? 'Downloading…' : 'Download'}
        </Button>
      </div>

      {downloadMutation.error && (
        <p role="alert" className="text-sm text-destructive">
          {downloadMutation.error.message}
        </p>
      )}
    </div>
  )
}
