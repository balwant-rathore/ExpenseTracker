import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import type { ApiError } from '@/lib/apiClient'
import { useAuthStore } from '@/store/authStore'
import { openBlobInNewTab } from '@/lib/blobDownload'
import { viewAttachment } from '../api/attachmentApi'

interface ViewReceiptLinkProps {
  attachmentId: string
  fileName: string
}

export function ViewReceiptLink({ attachmentId, fileName }: ViewReceiptLinkProps) {
  const accessToken = useAuthStore((state) => state.accessToken)
  const [popupBlocked, setPopupBlocked] = useState(false)

  const viewMutation = useMutation<Blob, ApiError, void>({
    mutationFn: async () => {
      const { blob } = await viewAttachment(attachmentId, accessToken)
      return blob
    },
    onSuccess: (blob) => {
      setPopupBlocked(openBlobInNewTab(blob) === null)
    },
  })

  return (
    <div className="flex flex-col gap-1">
      <Button
        type="button"
        variant="link"
        className="h-auto justify-start p-0"
        onClick={() => viewMutation.mutate()}
        disabled={viewMutation.isPending}
      >
        {viewMutation.isPending ? 'Opening…' : `View Receipt (${fileName})`}
      </Button>

      {viewMutation.error && (
        <p role="alert" className="text-sm text-destructive">
          {viewMutation.error.message}
        </p>
      )}

      {popupBlocked && viewMutation.data && (
        <p className="text-sm text-muted-foreground">
          Your browser blocked the receipt from opening —{' '}
          <button
            type="button"
            className="underline"
            onClick={() => setPopupBlocked(openBlobInNewTab(viewMutation.data!) === null)}
          >
            click here to try again
          </button>
          .
        </p>
      )}
    </div>
  )
}
