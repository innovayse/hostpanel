/**
 * The one test of whether a request is a browser opening a page.
 *
 * @module server/utils/documentNavigation
 */

import type { H3Event } from 'h3'
import { isBotUserAgent } from '~/server/utils/bot-agent'
import { HttpMethod } from '~/types/httpmethod'

/** The value of `Sec-Fetch-Dest` for a navigation to a page. */
const FETCH_DEST_DOCUMENT = 'document'

/** The media type a page navigation asks for. */
const HTML_MEDIA_TYPE = 'text/html'

/**
 * Whether the request is a browser opening a page: a GET, from a user agent that is a browser,
 * that the browser marks as a document or that accepts HTML.
 *
 * @param event - The incoming request.
 * @returns True for a document navigation.
 */
export const isDocumentNavigation = (event: H3Event): boolean => {
  if (event.method !== HttpMethod.Get || isBotUserAgent(getHeader(event, 'user-agent'))) {
    return false
  }
  const fetchDest = getHeader(event, 'sec-fetch-dest')
  if (fetchDest) {
    return fetchDest === FETCH_DEST_DOCUMENT
  }
  return (getHeader(event, 'accept') ?? '').includes(HTML_MEDIA_TYPE)
}
