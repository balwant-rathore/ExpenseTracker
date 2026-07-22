import { z } from 'zod'

export const rejectionCommentSchema = z.object({
  rejectionComment: z
    .string()
    .trim()
    .min(1, 'A comment is required')
    .max(500, 'Maximum 500 characters'),
})

export type RejectionCommentInput = z.infer<typeof rejectionCommentSchema>
