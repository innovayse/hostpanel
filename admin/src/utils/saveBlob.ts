/**
 * Hands a file the page already holds to the browser as a download.
 *
 * @module utils/saveBlob
 */

/**
 * Saves a blob under a file name, the way a download link would.
 *
 * The link is never attached to the page: a detached anchor's `click()` starts the download in
 * every current browser. The object URL is released straight after, since the browser has taken
 * its own reference by then.
 *
 * @param blob - The file's contents.
 * @param fileName - The name the browser saves it under.
 */
export const saveBlob = (blob: Blob, fileName: string): void => {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url 
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}
