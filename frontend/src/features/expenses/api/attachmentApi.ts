import { apiRequest } from '@/lib/apiClient'

export interface AttachmentUploadResponse {
  attachmentId: string
}

export function uploadAttachment(file: File, accessToken?: string | null): Promise<AttachmentUploadResponse> {
  const formData = new FormData()
  formData.append('file', file)
  return apiRequest<AttachmentUploadResponse>('/attachments', {
    method: 'POST',
    body: formData,
    accessToken,
  })
}
