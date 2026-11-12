/**
 * Helper for tests of screens that save a file: observes the download where it reaches the
 * browser (an object URL made for the blob, then a click on a detached link with a `download`
 * name) instead of replacing the code that does it. The test environment has no download
 * manager, so the browser end is stubbed and records what it was handed.
 */
import { vi } from 'vitest'

/** One download as the browser received it. */
export interface SavedDownload {
  /** The name on the link's `download` attribute. */
  fileName: string
  /** The file's contents. */
  blob: Blob
}

/**
 * Stubs the browser's download entry points. The object URL global is restored by
 * `vi.unstubAllGlobals()` and the link click by `vi.restoreAllMocks()`; a suite calls both after
 * each test.
 *
 * @returns The downloads started so far; the list fills as the page saves files.
 */
export const stubDownloads = (): SavedDownload[] => {
  /** Downloads started, in order. */
  const saved: SavedDownload[] = []
  /** Blobs by the object URL made for them. */
  const blobs = new Map<string, Blob>()
  vi.stubGlobal('URL', Object.assign(URL, {
    createObjectURL: (blob: Blob): string => {
      const url = `blob:download-${blobs.size}`
      blobs.set(url, blob)
      return url
    },
    revokeObjectURL: (): void => undefined,
  }))
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement): void {
    const blob = blobs.get(this.href)
    if (blob === undefined) {
      throw new Error(`A link was clicked for ${this.href}, which is not an object URL made for a blob`)
    }
    saved.push({ fileName: this.download, blob })
  })
  return saved
}
