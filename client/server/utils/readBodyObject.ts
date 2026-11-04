/**
 * Request-body guard for the Nitro routes.
 *
 * `readBody` hands back whatever the browser sent: nothing, a string, a number, an array. The
 * routes forward the body to the backend as a JSON object, so anything else is refused here with
 * a coded 400 and no sentence (the BFF writes none).
 *
 * @module server/utils/readBodyObject
 */

import type { H3Event } from 'h3'

/** The `data.code` of the 400 a body that is not a JSON object answers with. */
export const BAD_BODY = 'BAD_BODY'

/**
 * Reads the request body and refuses the request when it is not a JSON object.
 *
 * @param event - H3 event of the incoming request.
 * @returns The body as an object.
 * @throws H3Error 400 carrying `data.code = BAD_BODY` and no `statusMessage`.
 */
export const readBodyObject = async (event: H3Event): Promise<Record<string, unknown>> => {
  const body: unknown = await readBody(event)
  if (body === null || typeof body !== 'object' || Array.isArray(body)) {
    throw createError({ statusCode: 400, data: { code: BAD_BODY } })
  }
  return body as Record<string, unknown>
}

/**
 * Reads the request body and requires the named members to be strings.
 *
 * A route that forwards an identifier to the backend must not pass on a number, an object or a
 * missing member as if it were one, so each is checked here; the backend still decides whether the
 * value is meaningful.
 *
 * @param event - H3 event of the incoming request.
 * @param names - The members that must be present as strings.
 * @returns The named members, each a string.
 * @throws H3Error 400 carrying `data.code = BAD_BODY` and no `statusMessage` when the body is not an object or a member is not a string.
 */
export const readBodyStrings = async <K extends string>(event: H3Event, ...names: K[]): Promise<Record<K, string>> => {
  const body = await readBodyObject(event)
  const picked = {} as Record<K, string>
  for (const name of names) {
    const value = body[name]
    if (typeof value !== 'string') {
      throw createError({ statusCode: 400, data: { code: BAD_BODY } })
    }
    picked[name] = value
  }
  return picked
}
