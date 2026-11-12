/**
 * Helper for tests of screens that save a file: observes the download where it reaches the browser
 * (an object URL made for the blob, then a click on a detached link with a `download` name) instead
 * of replacing the code that does it. The test environment has no download manager, so only the two
 * browser primitives the download ends in are replaced, and what they were handed is recorded.
 *
 * The client keeps the same helper in `client/tests/helpers/captureDownloads.ts`: the two apps cannot
 * import from each other, so each carries its own copy of this one model.
 *
 * @module tests/helpers/captureDownloads
 */
import { vi } from 'vitest'

/** One download as the browser received it. */
interface SavedDownload {
  /** The name on the link's `download` attribute. */
  fileName: string
  /** The file's contents. */
  blob: Blob
}

/**
 * Starts recording downloads. The caller restores the primitives with `vi.restoreAllMocks()` after
 * the test; `URL.revokeObjectURL` stays a spy, so a test can assert which object URL was released.
 *
 * @returns The list that fills, oldest first, with every file saved from now on.
 */
export const captureDownloads = (): SavedDownload[] => {
  /** Downloads started, in order. */
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
    saved.push({ fileName: this.download, blob })
  })
  return saved
}
