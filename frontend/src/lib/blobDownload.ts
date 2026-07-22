export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

export function openBlobInNewTab(blob: Blob): Window | null {
  const url = URL.createObjectURL(blob)
  return window.open(url, '_blank')
  // Intentionally not revoked immediately — the new tab needs the object URL to remain valid
  // while it renders; the browser releases it when that tab/window is closed or navigated away.
}
