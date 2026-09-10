/**
 * Reads the sentence an API refusal carried, for any store that shows one.
 *
 * Every refusal on the API answers `{ error, code }`, and `error` sits next to the
 * rule that produced it — so when the rule changes the sentence changes with it,
 * which a copy kept in a store never does. `authErrorMessages.ts` applies the same
 * rule to sign-in and adds status fallbacks; this is the general half.
 */

import { ApiError } from '../composables/useApi'

/**
 * Extracts the `error` sentence from a failed response body.
 *
 * @param body - Parsed JSON body of a failed response, whatever shape it turned out to be.
 * @returns The server's own sentence, or null when the body carried none.
 */
export const bodyMessage = (body: unknown): string | null => {
  if (typeof body !== 'object' || body === null) return null
  const error = (body as { error?: unknown }).error
  return typeof error === 'string' && error.length > 0 ? error : null
}

/**
 * The sentence to show for a rejected request: the API's own wording when it wrote
 * one, the caller's fallback otherwise.
 *
 * @param cause - The value caught from a rejected {@link useApi} request.
 * @param fallback - Shown when the response carried no sentence, or never arrived.
 * @returns A human-readable message, never an empty string.
 */
export const apiErrorMessage = (cause: unknown, fallback: string): string => {
  if (cause instanceof ApiError) return bodyMessage(cause.body) ?? fallback
  return fallback
}
