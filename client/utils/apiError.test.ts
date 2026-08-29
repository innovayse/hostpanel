/**
 * Tests for reading a failed portal call: the code a page branches on, and the sentence it
 * renders.
 *
 * This replaces `portalErrorMessages.test.ts`. Its two `portalErrorMessageKey` cases are gone
 * with the mapping table they covered — the API now sends the finished sentence in the caller's
 * language, so there is nothing left in this frontend that turns a code into wording. The five
 * cases that survive test the two readers, which did not change and are what every page uses.
 *
 * The seam is still the one that carries the invitation refusal to the page. The Nitro route
 * itself is not importable here — `defineEventHandler` and friends are Nitro auto-imports that
 * exist only inside a Nitro build — so what the route contributes is asserted at the boundary it
 * hands its answer to: an H3 error's `data.code`, reshaped once by `server/error.ts` before the
 * browser sees it, read back out by {@link apiErrorCode}.
 *
 * The double-wrapping in the "through a Nuxt server route" case is not incidental. It is why
 * `apiErrorCode` checks two shapes, and a regression there would silently turn the
 * sign-in-required branch off — the page would keep rendering *something*, which is exactly the
 * failure mode that makes it worth a test.
 *
 * @module utils/apiError.test
 */

import { describe, expect, it } from 'vitest'
import { PortalErrorCode, apiErrorCode, apiErrorMessage } from './apiError'

describe('apiErrorCode', () => {
  it('reads the code out of a rejection that came through a Nuxt server route', () => {
    // What `apiFetch` throws when `accept-invite.post.ts` refuses: Nitro's envelope in
    // `data`, and the route's own `data` nested inside it.
    const err = { data: { statusCode: 403, data: { code: 'INVITE_SIGN_IN_REQUIRED' } } }

    expect(apiErrorCode(err)).toBe(PortalErrorCode.InviteSignInRequired)
  })

  it('reads the code out of a body that was not re-wrapped', () => {
    expect(apiErrorCode({ data: { code: 'CLIENT_PROFILE_NOT_FOUND' } }))
      .toBe(PortalErrorCode.ClientProfileNotFound)
  })

  it('answers null when the response carried no code at all', () => {
    // An expired or already-accepted invitation lands here: `AuthController` catches its own
    // `InvalidOperationException` and answers `{ error }` with no `code`, so the page has to
    // fall back to the sentence rather than branch. A test that expected a code here would
    // be asserting a contract the backend does not have.
    expect(apiErrorCode({ data: { statusCode: 400, message: 'This invitation has expired.' } }))
      .toBeNull()
    expect(apiErrorCode(new Error('offline'))).toBeNull()
  })
})

describe('apiErrorMessage', () => {
  it('quotes the API sentence rather than composing one', () => {
    // The expired-invitation path end to end. The sentence arrives already translated — the
    // backend resolved it from ValidationMessages.resx in the culture Accept-Language asked for —
    // so there is nothing for this side to look up.
    const err = { data: { statusMessage: 'Срок действия этого приглашения истёк.' } }

    expect(apiErrorMessage(err)).toBe('Срок действия этого приглашения истёк.')
  })

  it('falls back to one shared sentence when the request got no answer', () => {
    expect(apiErrorMessage({})).toBe('Could not reach the server.')
  })
})
