import { z } from 'zod'

const ALLOWED_EXTENSIONS = ['.pdf', '.jpg', '.jpeg', '.png']
const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024

function fileExtension(fileName: string): string {
  const index = fileName.lastIndexOf('.')
  return index === -1 ? '' : fileName.slice(index).toLowerCase()
}

export const attachmentFileSchema = z
  .instanceof(File)
  .refine((file) => ALLOWED_EXTENSIONS.includes(fileExtension(file.name)), {
    error: 'File type must be PDF, JPG, or PNG.',
  })
  .refine((file) => file.size <= MAX_FILE_SIZE_BYTES, {
    error: 'File size must not exceed 10 MB.',
  })

/** Used imperatively by AttachmentPicker on file selection, outside RHF's own resolver. */
export function validateAttachmentFile(file: File): string | null {
  const result = attachmentFileSchema.safeParse(file)
  return result.success ? null : (result.error.issues[0]?.message ?? 'Invalid file.')
}
