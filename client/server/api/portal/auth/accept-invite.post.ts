/**
 * POST /api/portal/auth/accept-invite
 *
 * Completes a client invitation: the account that is signed in right now is linked to the
 * client that issued the invitation, with the permissions the invitation carried.
 *
 * This route did not exist. `pages/client/accept-invite.vue` has posted here since it was
 * written and every one of those posts was answered by Nitro's own 404 — nobody has ever
 * completed an invitation through the portal.
 *
 * **The backend decides who accepts, not this route.** `POST /api/auth/accept-invite` is
 * `[Authorize]` and its command carries a token and nothing else
 * (`AcceptInvitationCommand(string Token)`); the handler asks the credential who the caller
 * is. So the only thing that travels from here is the token, and a request with no
 * `auth_token` cookie cannot succeed no matter what it carries — which is why that case is
 * answered here rather than forwarded.
 *
 * **It takes no password.** The page's password fields had nowhere to go: there is no
 * endpoint in this product that sets a password from an invitation token, and the invited
 * person gets their credentials from the identity provider. Forwarding a password to an
 * endpoint that ignores it would have told them their password was set when nothing had
 * read it.
 *
 * @module server/api/portal/auth/accept-invite.post
 */

import { PortalErrorCode } from '~/utils/apiError'

/** Body this route accepts. The password the page used to send is deliberately not here. */
interface AcceptInviteBody {
  /** The invitation token lifted out of the `?token=` query of the invitation mail's link. */
  token?: string
}

/** What the backend answers with on success. */
interface AcceptInviteResult {
  /** Always `true`; the backend returns `Ok(new { success = true })`. */
  success: boolean
}

/**
 * Builds the refusal for a visitor who has not signed in yet.
 *
 * Answered **403, not 401**, on purpose. `apiFetch` treats a 401 as "the session is dead",
 * hands off to `$handleAuthExpired` and navigates away — which would throw the invitation
 * token off the page before anyone could act on it. Nobody's session expired here; the
 * visitor simply arrived from their mail client without one, and the page has to keep the
 * token in hand while it sends them to sign in.
 *
 * @returns The H3 error to throw, carrying the code the page branches on.
 */
function signInRequired() {
  return createError({
    statusCode: 403,
    statusMessage: 'Sign in before accepting this invitation.',
    // `data.code` is the only part of an H3Error that reaches the browser — `server/error.ts`
    // exists to keep it — and it is what the page branches on to show the sign-in step.
    //
    // This is the one refusal in the portal whose sentence is not the API's, because the API
    // never sees the request: the page words it from `client.acceptInvite.signInRequired`.
    // `statusMessage` above is for the server log and for a non-browser caller, not for the
    // screen.
    data: { code: PortalErrorCode.InviteSignInRequired },
  })
}

/**
 * Forwards the invitation token to the backend on behalf of the signed-in caller.
 *
 * Errors are not reworded here. An expired or already-accepted invitation is an ordinary
 * outcome that `AuthController.AcceptInviteAsync` answers 400 for, and `internalApiCall`
 * carries the backend's own sentence across in `statusMessage` for the page to render. The
 * one refusal this route words itself is the one the backend never sees.
 */
export default defineEventHandler(async (event): Promise<AcceptInviteResult> => {
  const body = await readBody<AcceptInviteBody>(event)
  const token = typeof body?.token === 'string' ? body.token : ''

  // The only guard made before a request is possible. Everything else — an empty token
  // included — goes to the backend so the wording stays where the rule is.
  if (!getCookie(event, 'auth_token')) {
    throw signInRequired()
  }

  try {
    return await internalApiCall<AcceptInviteResult>(event, '/auth/accept-invite', {
      method: 'POST',
      body: { token },
    })
  } catch (err: unknown) {
    // A cookie that is present but no longer good reaches the same dead end as no cookie at
    // all: `internalApiCall` does not refresh for `/auth/*` endpoints, so this 401 is final.
    // The page needs the same recovery action either way.
    if ((err as { statusCode?: number })?.statusCode === 401) {
      throw signInRequired()
    }
    throw err
  }
})
