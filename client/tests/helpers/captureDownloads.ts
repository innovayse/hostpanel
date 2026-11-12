/**
 * Records the files a page hands to the browser as downloads, for store tests that save a PDF.
 *
 * `utils/saveBlob` runs for real: it makes an object URL and clicks a detached link. Only the
 * browser primitives it ends in are replaced (the object URL is not backed by a real file in the
 * test DOM and a click on a download link must not navigate), so a test asserts on the name and the
 * bytes the browser would have received.
 *
 * The admin keeps the same helper in `admin/tests/helpers/captureDownloads.ts`: the two apps cannot
 * import from each other, so each carries its own copy of this one model.
 *
 * @module tests/helpers/captureDownloads
 */

import { vi } from 'vitest'
import type { StubReply } from './stubServer'

/** The stub's answer for a PDF: the bytes with the type that makes the transport read them as a blob. */
export const PDF_REPLY: StubReply = { headers: { 'content-type': 'application/pdf' }, body: Buffer.from('%PDF-') }

/** One file the browser was asked to save. */
interface SavedDownload {
  /** The contents handed to the browser. */
  blob: Blob
  /** The name the browser would save it under. */
  fileName: string
}

/**
 * Starts recording downloads. The caller restores the primitives with `vi.restoreAllMocks()` after
 * the test; `URL.revokeObjectURL` stays a spy, so a test can assert which object URL was released.
 *
 * @returns The list that fills, oldest first, with every file saved from now on.
 */
export const captureDownloads = (): SavedDownload[] => {
  /** Files saved, in order. */
  const saved: SavedDownload[] = []
  /** Blobs by the object URL made for them. */
  const blobs = new Map<string, Blob>()
  vi.spyOn(URL, 'createObjectURL').mockImplementation((object: Blob | MediaSource): string => {
    const url = `blob:download-${blobs.size}`
    blobs.set(url, object as Blob)
    return url
  })
  vi.spyOn(URL, 'revokeObjectURL').mockImplementation((): void => undefined)
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement): void {
    const blob = blobs.get(this.href)
    if (blob === undefined) {
      throw new Error(`A link was clicked for ${this.href}, which is not an object URL made for a blob`)
    }
    saved.push({ blob, fileName: this.download })
  })
  return saved
}
