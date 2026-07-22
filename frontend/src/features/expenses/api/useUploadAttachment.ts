import { useMutation } from '@tanstack/react-query'
import type { ApiError } from '@/lib/apiClient'
import { useAuthStore } from '@/store/authStore'
import { uploadAttachment, type AttachmentUploadResponse } from './attachmentApi'

export function useUploadAttachment() {
  const accessToken = useAuthStore((state) => state.accessToken)

  return useMutation<AttachmentUploadResponse, ApiError, File>({
    mutationFn: (file) => uploadAttachment(file, accessToken),
  })
}
