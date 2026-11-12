/**
 * Tests for `pages/client/accept-invite.vue` against the real auth store, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the
 * missing-token state, the sign-in-required step and a plain refusal.
 *
 * @module tests/pages/client/accept-invite.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountAuthPage, stubAuthRuntime } from '~/tests/helpers/authScreens'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import AcceptInvitePage from '~/pages/client/accept-invite.vue'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF path the page posts the invitation token to. */
const ACCEPT_PATH = '/api/portal/auth/accept-invite'

/** What the BFF answers when nobody is signed in: not a failure, the cue to offer the sign-in step. */
const SIGN_IN_REQUIRED = { status: 403, body: { type: PortalErrorCode.InviteSignInRequired } }

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Submits the accept form of a mounted page.
 *
 * @param wrapper - The mounted page.
 */
const submit = async (wrapper: Awaited<ReturnType<typeof mountAuthPage>>): Promise<void> => {
  await wrapper.find('form').trigger('submit')
}

/**
 * Submits the accept form and waits until the page shows the sign-in step.
 *
 * @param wrapper - The mounted page.
 */
const submitUntilSignInRequired = async (wrapper: Awaited<ReturnType<typeof mountAuthPage>>): Promise<void> => {
  await submit(wrapper)
  await waitUntil(() => expect(wrapper.find('[data-testid="invite-sign-in-required"]').exists()).toBe(true))
}

beforeEach(async () => {
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('client accept-invite page', () => {
  it('shows the invalid-link state when the link has no token', async () => {
    stubAuthRuntime()

    const wrapper = await mountAuthPage(AcceptInvitePage)

    expect(wrapper.find('[data-testid="invite-invalid"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invite-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invite-invalid-back"]').attributes('href')).toBe('/client/login')
  })

  it('shows the accept form for a link with a token', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })

    const wrapper = await mountAuthPage(AcceptInvitePage)

    expect(wrapper.find('[data-testid="invite-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invite-submit"]').exists()).toBe(true)
  })

  it('posts the token when the form is submitted', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })
    table[`POST ${ACCEPT_PATH}`] = { status: 400, body: { detail: 'This invitation has expired.' } }
    const wrapper = await mountAuthPage(AcceptInvitePage)

    await submit(wrapper)
    await waitUntil(() => expect(requestsTo(stub.requests, ACCEPT_PATH)).toHaveLength(1))

    expect(bodyOf(requestsTo(stub.requests, ACCEPT_PATH)[0])).toEqual({ token: 'tok' })
  })

  it('shows the API sentence for a refused invitation', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })
    table[`POST ${ACCEPT_PATH}`] = { status: 400, body: { detail: 'This invitation has expired.' } }
    const wrapper = await mountAuthPage(AcceptInvitePage)

    await submit(wrapper)
    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-error"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="ui-form-error"]').text()).toBe('This invitation has expired.')
  })

  it('offers the sign-in step when the API says a session is needed', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })
    table[`POST ${ACCEPT_PATH}`] = { status: 403, body: { type: PortalErrorCode.InviteSignInRequired, detail: 'Sign in first.' } }
    const wrapper = await mountAuthPage(AcceptInvitePage)

    await submitUntilSignInRequired(wrapper)

    expect(wrapper.find('[data-testid="invite-form"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('client.acceptInvite.signInRequired')
  })

  it('shows the accept form again when the visitor returns to the link after a sign-in-required refusal', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })
    table[`POST ${ACCEPT_PATH}`] = SIGN_IN_REQUIRED
    const first = await mountAuthPage(AcceptInvitePage)
    await submitUntilSignInRequired(first)

    const second = await mountAuthPage(AcceptInvitePage)

    expect(second.find('[data-testid="invite-form"]').exists()).toBe(true)
    expect(second.find('[data-testid="invite-sign-in-required"]').exists()).toBe(false)
  })

  it('sends the visitor to the local sign-in page and back to this exact link', async () => {
    stubAuthRuntime({ query: { token: 'tok' }, fullPath: '/client/accept-invite?token=tok' })
    table[`POST ${ACCEPT_PATH}`] = SIGN_IN_REQUIRED
    const wrapper = await mountAuthPage(AcceptInvitePage)
    await submitUntilSignInRequired(wrapper)

    await wrapper.find('[data-testid="invite-sign-in"]').trigger('click')

    expect(navigateTo).toHaveBeenCalledWith(`/client/login?redirect=${encodeURIComponent('/client/accept-invite?token=tok')}`)
  })

  it('sends the visitor to the identity provider under SSO, with the return address', async () => {
    stubAuthRuntime({
      query: { token: 'tok' },
      fullPath: '/client/accept-invite?token=tok',
      isSso: true,
      publicConfig: { baseUrl: 'https://portal.example.test' }
    })
    table[`POST ${ACCEPT_PATH}`] = SIGN_IN_REQUIRED
    const wrapper = await mountAuthPage(AcceptInvitePage)
    await submitUntilSignInRequired(wrapper)

    await wrapper.find('[data-testid="invite-sign-in"]').trigger('click')

    expect(navigateTo).toHaveBeenCalledWith(
      `https://portal.example.test/api/portal/auth/sso/authorize?returnTo=${encodeURIComponent('/client/accept-invite?token=tok')}`,
      { external: true }
    )
  })
})
