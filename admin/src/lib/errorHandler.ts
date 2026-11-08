/**
 * The one reader of a rejected API call in the admin SPA.
 *
 * Every refusal on the API answers `application/problem+json`:
 * `{ type, title, status, detail, traceId, errors? }`. `type` is the machine-readable error
 * code (never localized, the only thing to branch on), `detail` is the finished sentence in
 * the language named by `X-Language`, and `errors` maps camelCase field names to the messages
 * for that field. No store or view picks members off a body itself.
 *
 * The client authors two sentences, all in the locale catalogues: `errors.noAnswer`
 * (no answer at all, or an answer without any wording) and `errors.signInLoop` (the API
 * accepted a sign-in but will not accept the session back, which no response describes).
 * There is no status-to-sentence table.
 */

import { ApiError } from '@/composables/useApi'
import { translate } from '@/lib/translate'

/**
 * Reads a non-empty string member off an unknown body.
 *
 * @param body - Parsed JSON body of a failed response, whatever shape it turned out to be.
 * @param key - The member to read.
 * @returns The member's value, or null when absent, not a string, or empty.
 */
const stringMember = (body: unknown, key: string): string | null => {
  if (typeof body !== 'object' || body === null) {
    return null
  }
  const value = (body as Record<string, unknown>)[key]
  return typeof value === 'string' && value.length > 0 ? value : null
}

/**
 * The sentence to show for a rejected request: the API's `detail`, then its `title`, then the
 * single shared "no answer" sentence when the response carried no wording or never arrived.
 *
 * @param cause - The value caught from a rejected {@link useApi} request.
 * @returns A human-readable message, never an empty string.
 */
export const errorSentence = (cause: unknown): string => {
  if (cause instanceof ApiError) {
    const wording = stringMember(cause.body, 'detail') ?? stringMember(cause.body, 'title')
    if (wording !== null) {
      return wording
    }
  }
  return translate('errors.noAnswer')
}

/**
 * The machine-readable code (`type`) of a rejected request, for branching.
 *
 * @param cause - The value caught from a rejected {@link useApi} request.
 * @returns The code, e.g. `VALIDATION_ERROR`, or null when the response carried none or never arrived.
 */
export const errorCode = (cause: unknown): string | null =>
  cause instanceof ApiError ? stringMember(cause.body, 'type') : null

/**
 * The HTTP status of a rejected request, for branching where the status itself is the answer (a 404).
 *
 * @param cause - The value caught from a rejected {@link useApi} request.
 * @returns The status, or null when the request never got an answer.
 */
export const errorStatus = (cause: unknown): number | null => (cause instanceof ApiError ? cause.status : null)

/**
 * The per-field messages of a rejected request, keyed by camelCase field name; a field of a nested
 * object is keyed by its dotted path with every segment camelCased (`endpoint.hostname`).
 *
 * @param cause - The value caught from a rejected {@link useApi} request.
 * @returns Field name to its first message; an empty object when the response named no fields.
 */
export const fieldErrors = (cause: unknown): Record<string, string> => {
  const result: Record<string, string> = {}
  if (!(cause instanceof ApiError) || typeof cause.body !== 'object' || cause.body === null) {
    return result
  }
  const fields = (cause.body as Record<string, unknown>).errors
  if (typeof fields !== 'object' || fields === null) {
    return result
  }
  for (const [name, messages] of Object.entries(fields)) {
    const first = Array.isArray(messages) ? messages[0] : messages
    if (typeof first === 'string' && first.length > 0) {
      result[name.split('.').map(segment => segment.charAt(0).toLowerCase() + segment.slice(1)).join('.')] = first
    }
  }
  return result
}
