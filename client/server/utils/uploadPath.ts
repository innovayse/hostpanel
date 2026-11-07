/**
 * The one reader of the `/uploads/**` catch-all parameter.
 *
 * The routes under `/uploads` forward a visitor-chosen path to the file server of the backend, so
 * the path is the whole attack surface. This module decodes it exactly once, refuses everything
 * that could address anything but a plain file below the uploads folder, and hands back the path
 * re-encoded segment by segment, which is the only form a route may append to the upstream
 * address. Both `uploads/[...path].ts` and `uploads/branding/[...path].ts` use it, so the
 * two cannot drift apart.
 *
 * @module server/utils/uploadPath
 */

import type { H3Event } from 'h3'
import { BAD_ROUTE_PARAM } from '~/server/utils/requireRouteParam'

/** The highest code point of an ASCII control character that a path may never carry. */
const LAST_CONTROL_CODE = 0x1f

/** The DEL control character. */
const DELETE_CODE = 0x7f

/**
 * The refusal every malformed path answers with: a coded 400 and no sentence.
 *
 * @returns The H3 error to throw.
 */
const badPath = () => createError({ statusCode: 400, data: { code: BAD_ROUTE_PARAM } })

/**
 * Whether a character is an ASCII control character.
 *
 * @param character - A single character.
 * @returns True for NUL through US and DEL.
 */
const isControl = (character: string): boolean => {
  const code = character.charCodeAt(0)
  return code <= LAST_CONTROL_CODE || code === DELETE_CODE
}

/**
 * Reads the `path` catch-all parameter of an uploads route and refuses anything but a plain,
 * relative file path.
 *
 * The raw parameter is percent-decoded once. The decoded text is then refused when it is empty,
 * holds a backslash, a control character or a further `%` (a second decoding by the file server
 * would turn `%252e%252e` into `..`), or has an empty, `.` or `..` segment. The accepted segments
 * are encoded again one by one, so what reaches the upstream address is exactly what was checked.
 *
 * @param event - H3 event of the incoming request.
 * @returns The relative path, safe to append after the uploads folder of the backend.
 * @throws H3Error 400 carrying `data.code = BAD_ROUTE_PARAM` and no `statusMessage`.
 */
export const requireUploadPath = (event: H3Event): string => {
  const raw = getRouterParam(event, 'path') ?? ''

  let decoded: string
  try {
    decoded = decodeURIComponent(raw)
  } catch {
    throw badPath()
  }

  if (decoded === '' || decoded.includes('\\') || decoded.includes('%') || [...decoded].some(isControl)) {
    throw badPath()
  }

  const segments = decoded.split('/')
  if (segments.some(segment => segment === '' || segment === '.' || segment === '..')) {
    throw badPath()
  }

  return segments.map(encodeURIComponent).join('/')
}
