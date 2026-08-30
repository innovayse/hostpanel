/**
 * The one place an authentication failure turns into words for the operator.
 *
 * The rule this file exists to keep: the sentence comes from the API response body
 * whenever the API wrote one. The table below is not a second copy of those sentences
 * — it only covers the cases where the server had no body to give (a status alone, a
 * request that never arrived), so no page ever has to compose its own wording and no
 * template ever grows a chain of `v-else-if` over error codes.
 */

import { ApiError } from '../composables/useApi'

/**
 * Fallback wording by HTTP status, used only when the response carried no `error`
 * field of its own. Each entry says what the operator can do about it.
 */
const STATUS_FALLBACKS: Readonly<Record<number, string>> = {
  /** Local sign-in routes answer 404 when the deployment runs in SSO mode. */
  404: 'Password sign-in is not enabled on this deployment.',
  /** Credentials rejected without an explanatory body. */
  401: 'Those credentials were not accepted.',
  /** Signed in, but not allowed to do this. */
  403: 'Your account is not allowed to do that.',
  /** Setup already claimed, or a conflicting state. */
  409: 'That has already been done.',
  /**
   * Rate limited. Explicitly not "something went wrong" — the caller is going too
   * fast and waiting is the recovery action.
   */
  429: 'Too many attempts. Wait a moment and try again.',
  /** The API broke; nothing the operator typed caused it. */
  500: 'The server failed to handle that request.',
  /** The API is not answering behind the proxy. */
  502: 'The API is not reachable right now.',
  503: 'The API is not reachable right now.',
}

/**
 * Wording for a request that got no answer at all — offline, blocked, process gone.
 * There is no body to read in this case, so this is the one invented sentence, and it
 * lives here rather than in any page.
 */
const NO_RESPONSE = 'Could not reach the server. Check your connection and try again.'

/** Last resort for a status this table does not name. Surfaced, never swallowed. */
const UNKNOWN = 'That request failed for an unexpected reason.'

/**
 * Reads the message the API itself chose out of a failed response body.
 *
 * Both auth controllers answer failures as `{ "error": "…" }`, and that string sits
 * next to the rule that produced it — so when the rule changes the sentence changes
 * with it, which a copy kept in a page never does.
 *
 * @param body - Parsed JSON body of a failed response, whatever shape it turned out to be.
 * @returns The server's own sentence, or null when the body carried none.
 */
const bodyMessage = (body: unknown): string | null => {
  if (typeof body !== 'object' || body === null) return null
  const error = (body as { error?: unknown }).error
  return typeof error === 'string' && error.length > 0 ? error : null
}

/**
 * Turns any thrown value from the API layer into a sentence to show the operator.
 *
 * Order is deliberate: the server's own wording first, the status table second, the
 * generic line last. Nothing is swallowed — an unrecognised failure still produces
 * visible text.
 *
 * @param cause - The value caught from a rejected {@link useApi} request.
 * @returns A human-readable message, never an empty string.
 */
export const authErrorMessage = (cause: unknown): string => {
  if (cause instanceof ApiError) {
    return bodyMessage(cause.body) ?? STATUS_FALLBACKS[cause.status] ?? UNKNOWN
  }

  // Anything that is not an ApiError never reached the server — fetch itself rejected.
  if (cause instanceof TypeError) return NO_RESPONSE

  return UNKNOWN
}
