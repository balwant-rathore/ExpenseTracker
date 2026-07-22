import { useRef, useState, type ChangeEvent } from 'react'
import { Button } from '@/components/ui/button'
import { FieldError } from '@/components/ui/field'
import { validateAttachmentFile } from '../schemas/attachmentSchema'

export type AttachmentSelection =
  | { kind: 'none' }
  | { kind: 'existing'; attachmentId: string; fileName: string }
  | { kind: 'new'; file: File }

interface AttachmentPickerProps {
  value: AttachmentSelection
  onChange: (selection: AttachmentSelection) => void
  error?: string
}

export function AttachmentPicker({ value, onChange, error }: AttachmentPickerProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [localError, setLocalError] = useState<string | null>(null)

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) {
      return
    }

    const validationError = validateAttachmentFile(file)
    if (validationError) {
      setLocalError(validationError)
      if (inputRef.current) {
        inputRef.current.value = ''
      }
      return
    }

    setLocalError(null)
    onChange({ kind: 'new', file })
  }

  const currentFileName =
    value.kind === 'existing' ? value.fileName : value.kind === 'new' ? value.file.name : null
  const displayedError = localError ?? error

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center gap-2">
        <Button type="button" variant="outline" onClick={() => inputRef.current?.click()}>
          {currentFileName ? 'Replace receipt' : 'Select receipt'}
        </Button>
        {currentFileName && (
          <span className="text-sm text-muted-foreground">{currentFileName}</span>
        )}
      </div>
      <input
        ref={inputRef}
        type="file"
        accept=".pdf,.jpg,.jpeg,.png"
        aria-label="Receipt attachment"
        className="sr-only"
        onChange={handleFileChange}
      />
      <FieldError errors={displayedError ? [{ message: displayedError }] : undefined} />
    </div>
  )
}
