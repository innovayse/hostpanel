/**
 * Hands a PDF the page already holds to the browser's print flow.
 *
 * @module lib/printBlob
 */

/** How long the printed file's object URL and frame stay alive, in milliseconds; printing is asynchronous and has no reliable end event for a PDF. */
const PRINT_RELEASE_DELAY_MS = 60_000

/**
 * Opens a blob in a hidden frame and prints it as soon as it has loaded.
 *
 * The frame is never visible. The object URL and the frame are released after {@link PRINT_RELEASE_DELAY_MS}, since the
 * print dialog outlives the load and a PDF viewer does not report when it is done.
 *
 * @param blob - The file to print, a PDF.
 */
export const printBlob = (blob: Blob): void => {
  const url = URL.createObjectURL(blob)
  const frame = document.createElement('iframe')
  frame.hidden = true
  frame.addEventListener('load', () => {
    frame.contentWindow?.focus()
    frame.contentWindow?.print()
  })
  frame.src = url
  document.body.appendChild(frame)
  window.setTimeout(() => {
    frame.remove()
    URL.revokeObjectURL(url)
  }, PRINT_RELEASE_DELAY_MS)
}
