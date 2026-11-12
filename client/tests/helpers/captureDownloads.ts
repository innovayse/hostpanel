/**
 * Records the files a page hands to the browser as downloads, for store tests that save a PDF.
 *
 * `utils/saveBlob` runs for real: it makes an object URL and clicks a detached link. Only the two
 * browser primitives it ends in are replaced (the object URL is not backed by a real file in the
 * test DOM and a click on a download link must not navigate), so a test asserts on the name and the
 * bytes the browser would have received.
 *
 * @module tests/helpers/captureDownloads
 */

import { vi } from 'vitest'
import type { StubReply } from './stubServer'

/** The stub's answer for a PDF: the bytes with the type that makes the transport read them as a blob. */
export const PDF_REPLY: StubReply = { headers: { 'content-type': 'application/pdf' }, body: Buffer.from('%PDF-') }

/** One file the browser was asked to save. */
export interface SavedDownload {
  /** The contents handed to the browser. */
  blob: Blob
  /** The name the browser would save it under. */
  fileName: string
}

/**
 * Starts recording downloads. The caller restores the primitives with `vi.restoreAllMocks()` after the test.
 *
 * @returns The list that fills, oldest first, with every file saved from now on.
 */
export const captureDownloads = (): SavedDownload[] => {
  const saved: SavedDownload[] = []
  let lastBlob: Blob = new Blob()
  vi.spyOn(URL, 'createObjectURL').mockImplementation((object: Blob | MediaSource) => {
    lastBlob = object as Blob
    return 'blob:download'
  })
  vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined)
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
    saved.push({ blob: lastBlob, fileName: this.download })
  })
  return saved
}
