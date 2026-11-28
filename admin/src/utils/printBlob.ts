/**
 * Hands a PDF the page already holds to the browser's print flow.
 *
 * @module utils/printBlob
 */

/** How long the hidden frame and its object URL live: long enough for the print dialog to be used, then released. */
const PRINT_RELEASE_DELAY_MS = 60_000

/**
 * Prints a PDF without showing it: the blob is loaded into a hidden frame and the frame's print dialog opens once it has loaded.
 *
 * The frame and the object URL are released a minute later, since closing the dialog gives the page no event to release them on.
 *
 * @param blob - The PDF's contents.
 */
export const printBlob = (blob: Blob): void => {
  const url = URL.createObjectURL(blob)
  const frame = document.createElement('iframe')
  frame.className = 'fixed bottom-0 right-0 h-0 w-0 border-0'
  frame.setAttribute('aria-hidden', 'true')
  frame.addEventListener('load', () => {
    frame.contentWindow?.focus()
    frame.contentWindow?.print()
    setTimeout(() => {
      frame.remove()
      URL.revokeObjectURL(url)
    }, PRINT_RELEASE_DELAY_MS)
  }, { once: true })
  frame.src = url
  document.body.appendChild(frame)
}
