/**
 * The one reader of the visitor-chosen paths of the plugin proxy routes: the route catch-all and query of the plugin
 * call routes, and the page address of the page lookup.
 *
 * `server/api/portal/client/plugins/[plugin]/routes/[...path]` and `.../stream/[...path]` forward a visitor-chosen
 * path below the plugin's prefix to the backend, which matches it against the plugin's declared routes. The path is
 * split on `/` before decoding, so a route value holding an encoded `/` stays one segment; each segment is decoded
 * once, refused when it is empty, `.`, `..` or holds a backslash, a control character or a further `%`, and encoded
 * again, so what reaches the backend is exactly what was checked. The page address is a query value instead: it is
 * checked decoded and handed on decoded, so it is encoded exactly once, by the backend URL's query string.
 *
 * @module server/utils/pluginPath
 */

import type { H3Event } from 'h3'
import { BAD_ROUTE_PARAM } from '~/server/utils/requireRouteParam'

/** Characters a decoded segment may never hold: backslash, `%` (a second decoding) and ASCII control characters. */
// eslint-disable-next-line no-control-regex -- the control range is what this refuses
const FORBIDDEN = /[\\%\u0000-\u001f\u007f]/

/**
 * The refusal every malformed path answers with: a coded 400 and no sentence.
 *
 * @returns The H3 error to throw.
 */
const badPath = () => createError({ statusCode: 400, data: { code: BAD_ROUTE_PARAM } })

/**
 * Decodes, checks and re-encodes one segment.
 *
 * @param raw - The segment as it arrived.
 * @returns The segment, percent-encoded.
 * @throws H3Error 400 for a segment that is not a plain value.
 */
const checkedSegment = (raw: string): string => {
  let decoded: string
  try {
    decoded = decodeURIComponent(raw)
  } catch {
    throw badPath()
  }
  return encodeURIComponent(plainSegment(decoded))
}

/**
 * Checks one already decoded segment.
 *
 * @param decoded - The segment's text.
 * @returns The same text.
 * @throws H3Error 400 when it is empty, `.`, `..`, or holds a backslash, `%` or a control character.
 */
const plainSegment = (decoded: string): string => {
  if (decoded === '' || decoded === '.' || decoded === '..' || FORBIDDEN.test(decoded)) {
    throw badPath()
  }
  return decoded
}

/**
 * Reads the `path` catch-all of a plugin proxy route.
 *
 * @param event - H3 event of the incoming request.
 * @returns The path below the plugin's prefix, each segment percent-encoded.
 * @throws H3Error 400 carrying `data.code = BAD_ROUTE_PARAM` and no `statusMessage`.
 */
export const requirePluginPath = (event: H3Event): string => {
  const raw = getRouterParam(event, 'path', { decode: false }) ?? ''
  return raw.split('/').map(checkedSegment).join('/')
}

/** The longest page address the lookup forwards, in characters; a page path is a few short segments. */
const MAX_PAGE_PATH_LENGTH = 1024

/**
 * Reads the `path` query of the page lookup: the address below `/client/apps/<plugin>`, e.g. `/sites/blog`.
 *
 * The query value arrives decoded once and is forwarded decoded, as the backend's `path` query value; only the
 * backend URL's own query encoding encodes it, so a segment such as `կայք` or `сайт` reaches the matcher as written.
 *
 * @param event - H3 event of the incoming request.
 * @returns The address with a leading `/` (`/` alone for the root page), each decoded segment checked like a route
 *   segment.
 * @throws H3Error 400 carrying `data.code = BAD_ROUTE_PARAM` for a missing, repeated, overlong or malformed value.
 */
export const requirePluginPagePath = (event: H3Event): string => {
  const raw = getQuery(event).path
  // A repeated key arrives as an array; which one would win is not the visitor's to choose, so it is refused.
  if (typeof raw !== 'string' || !raw.startsWith('/') || raw.length > MAX_PAGE_PATH_LENGTH) {
    throw badPath()
  }
  // `/` alone is the plugin's root page (`/client/apps/<plugin>`); every other address is checked segment by segment.
  if (raw === '/') {
    return raw
  }
  return `/${raw.slice(1).split('/').map(plainSegment).join('/')}`
}

/**
 * The incoming query as a query string for the backend: string values only, repeated keys kept.
 *
 * @param event - H3 event of the incoming request.
 * @returns `?a=1&b=2`, or an empty string when there is no query.
 */
export const pluginQuery = (event: H3Event): string => {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(getQuery(event))) {
    for (const item of Array.isArray(value) ? value : [value]) {
      if (typeof item === 'string') {
        search.append(key, item)
      }
    }
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}
