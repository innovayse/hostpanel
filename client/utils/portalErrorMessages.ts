/**
 * The one place a backend error code turns into something a person reads.
 *
 * One mapping table: codes translate to human text here and nowhere else, never inline in a
 * template with a chain of `v-else-if`. Everything that renders a failed portal call goes
 * through the helpers here, so a code gets its wording once and every page agrees about it.
 *
 * The table stays deliberately small. A code only earns an entry when the portal must *do*
 * something different for it — offer a recovery action, drop the red styling. Every other
 * failure keeps rendering the sentence the API sent, because the rule that refused the
 * request is the only thing that knows why, and a copy kept here would go stale silently.
 *
 * @module utils/portalErrorMessages
 */

/**
 * Machine-readable codes the C# backend sends in the `code` field of its error body.
 *
 * SCREAMING_SNAKE, matching the backend. These strings cross the wire and are part of the
 * contract: renaming one here without renaming it in the backend silently turns the branch
 * off, with no error and no failing type-check.
 */
export const PortalErrorCode = {
  /**
   * The signed-in identity has no row in the backend's `clients` table.
   *
   * Not a failure. Staff identities — the platform superadmin above all — authenticate fine
   * and were simply never onboarded as customers, so every "my …" endpoint answers 404 with
   * this code. The portal renders an explanation and a way out, never a red alert.
   */
  ClientProfileNotFound: 'CLIENT_PROFILE_NOT_FOUND',
} as const

/** Any of the codes in {@link PortalErrorCode}. */
export type PortalErrorCodeValue = typeof PortalErrorCode[keyof typeof PortalErrorCode]

/**
 * The mapping table itself: backend code → i18n key.
 *
 * i18n keys rather than literal sentences because the portal ships in en/ru/hy and the
 * backend answers in English only. This is the one sanctioned copy of a server-side reason;
 * everything not listed here still renders the server's own wording.
 */
const MESSAGE_KEYS: Readonly<Record<string, string>> = {
  [PortalErrorCode.ClientProfileNotFound]: 'client.noProfile.body',
}

/**
 * Looks up the i18n key that explains a backend error code.
 *
 * @param code - The `code` the API sent, or `null` when it sent none.
 * @returns The i18n key to translate, or `null` when this code has no special wording and the
 *          server's own sentence should be shown instead.
 */
export function portalErrorMessageKey(code: string | null | undefined): string | null {
  return (code && MESSAGE_KEYS[code]) || null
}

/**
 * Reads the machine-readable code out of a rejected `apiFetch` call.
 *
 * Two shapes are checked because the body is reshaped once on the way out. A call that went
 * through a Nuxt server route arrives as Nitro's error envelope, whose `data` holds what
 * `internalApiCall` attached (`err.data.data.code`); a call that reached the C# API directly
 * carries its body unwrapped (`err.data.code`).
 *
 * @param err - Whatever `apiFetch` threw.
 * @returns The code, or `null` when the response carried none — including a request that
 *          never got an answer at all.
 */
export function apiErrorCode(err: unknown): string | null {
  const body = (err as { data?: { code?: string | null, data?: { code?: string | null } } })?.data
  return body?.data?.code ?? body?.code ?? null
}

/**
 * Reads the sentence to show from a failed API call.
 *
 * The wording comes from the response body and is never written here: the API is the only
 * side that knows why it refused, and a message invented in the client goes stale the moment
 * the endpoint's reasons change. The generic line is the last resort for a request that never
 * reached the API at all — an offline browser has no response body to quote — and it lives in
 * this one helper rather than being repeated per page.
 *
 * @param err - Whatever `apiFetch` threw.
 * @returns The message to display.
 */
export function apiErrorMessage(err: unknown): string {
  const body = (err as { data?: { message?: string, statusMessage?: string } })?.data
  return body?.message
    ?? body?.statusMessage
    ?? (err as { message?: string })?.message
    ?? 'Could not reach the server.'
}
