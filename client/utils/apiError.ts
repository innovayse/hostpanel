/**
 * Reading a failed portal call: the sentence to show, and the code to branch on.
 *
 * This file replaces `utils/portalErrorMessages.ts`, and the difference is the whole point:
 * there is **no mapping table here**. Codes are not translated into wording on this side any
 * more. The API answers `{ error, code }` where `error` is a finished sentence already written
 * in the caller's language — `Accept-Language` reaches
 * `Innovayse.Application/Resources/ValidationMessages*.resx` through `UseRequestLocalization` — so a
 * page renders `apiErrorMessage(err)` and is done.
 *
 * The old table existed because the backend answered in English only while the portal ships
 * en/ru/hy, and it covered five codes; every other refusal reached a Russian or Armenian
 * customer in English regardless. The wording now lives beside the rule that produced it, in one
 * place, for all three languages.
 *
 * **Codes did not go away.** They are still the contract, and still the only thing a page may
 * branch on — `stores/client.ts` decides whether an account is a customer account, and
 * `pages/client/accept-invite.vue` decides whether to offer the sign-in step. Branching on the
 * sentence would break the moment it is reworded, and it is now reworded in three languages.
 * This mirrors innovayse-sso, whose store holds `auth.error` for rendering and `auth.errorCode`
 * for deciding.
 *
 * @module utils/apiError
 */

/**
 * Machine-readable codes the API sends in the `code` field of its error body.
 *
 * SCREAMING_SNAKE, matching the backend. These strings cross the wire and are part of the
 * contract: renaming one here without renaming it in the backend silently turns the branch off,
 * with no error and no failing type-check.
 *
 * Only codes something actually branches on are listed. A code that merely needs displaying
 * needs no entry, because displaying it means rendering `error` and nothing else.
 */
export const PortalErrorCode = {
  /**
   * The signed-in identity has no row in the backend's `clients` table.
   *
   * Not a failure. Staff identities — the platform superadmin above all — authenticate fine and
   * were simply never onboarded as customers, so every "my …" endpoint answers 404 with this
   * code. `stores/client.ts` turns it into a state flag so the portal renders an explanation and
   * a way out instead of a red alert; the explanation itself is the sentence the API sent.
   */
  ClientProfileNotFound: 'CLIENT_PROFILE_NOT_FOUND',

  /**
   * An invitation was opened by someone who is not signed in.
   *
   * Not a failure either, and not an expired session. The invitation mail lands in a mail
   * client, so arriving here with no credential is the normal first visit;
   * `POST /api/auth/accept-invite` is `[Authorize]` and resolves *who* accepted from the
   * credential, so there is nothing to send until the visitor has one.
   *
   * **This one code is written by the BFF, not by the C# API** — `server/api/portal/auth/
   * accept-invite.post.ts` refuses before any request to the backend is possible, so the
   * backend never sees it and has no resource entry for it. Its sentence therefore stays in the
   * portal's own `client.acceptInvite.signInRequired`, rendered by the page that branches on
   * this code. That is `api-driven-frontend.md`'s second sanctioned exception — a guard before
   * any request is possible — and it is the only sentence about a refusal left in this frontend.
   */
  InviteSignInRequired: 'INVITE_SIGN_IN_REQUIRED',
} as const

/** Any of the codes in {@link PortalErrorCode}. */
export type PortalErrorCodeValue = typeof PortalErrorCode[keyof typeof PortalErrorCode]

/**
 * Reads the machine-readable code out of a rejected `apiFetch` call.
 *
 * Two shapes are checked because the body is reshaped once on the way out. A call that went
 * through a Nuxt server route arrives as Nitro's error envelope, whose `data` holds what
 * `internalApiCall` attached (`err.data.data.code`); a call that reached the C# API directly
 * carries its body unwrapped (`err.data.code`).
 *
 * @param err - Whatever `apiFetch` threw.
 * @returns The code, or `null` when the response carried none — including a request that never
 *          got an answer at all.
 */
export function apiErrorCode(err: unknown): string | null {
  const body = (err as { data?: { code?: string | null, data?: { code?: string | null } } })?.data
  return body?.data?.code ?? body?.code ?? null
}

/**
 * Reads the sentence to show from a failed API call.
 *
 * The wording comes from the response body and is never written here: the API is the only side
 * that knows why it refused, it is the side that has the translations, and a message invented in
 * the client goes stale the moment the endpoint's reasons change. The generic line is the last
 * resort for a request that never reached the API at all — an offline browser has no response
 * body to quote — and it lives in this one helper rather than being repeated per page, which is
 * the first of the two exceptions `api-driven-frontend.md` allows.
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
