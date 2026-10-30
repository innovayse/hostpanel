/**
 * Types for a file the API hands over for the page to save.
 *
 * @module types/download
 */

/** A file the API answered with, and the name it gave it. */
export interface DownloadedFile {
  /** The file's contents. */
  blob: Blob
  /** The name from the `Content-Disposition` header, null when the response gave none. */
  fileName: string | null
}
