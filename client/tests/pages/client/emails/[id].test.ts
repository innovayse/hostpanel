/**
 * Tests for `pages/client/emails/[id].vue` against the real client store, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the message in
 * a frame and the not-found state.
 *
 * @module tests/pages/client/emails/[id].test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import EmailPage from '~/pages/client/emails/[id].vue'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF path the page reads for email `e1`. */
const EMAIL_PATH = '/api/portal/client/emails/e1'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

beforeEach(async () => {
  stubClientRuntime({ params: { id: 'e1' } })
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('email page', () => {
  it('shows the subject and the body in a sandboxed frame', async () => {
    table[`GET ${EMAIL_PATH}`] = { body: { id: 1, to: 'a@b.test', subject: 'Welcome', sentAt: '2026-01-01T10:00:00Z', body: '<p>Hello</p>', success: true } }

    const wrapper = await mountPage(EmailPage)
    await waitUntil(() => expect(wrapper.find('iframe').exists()).toBe(true))

    expect(requestsTo(stub.requests, EMAIL_PATH).map(request => request.method)).toEqual(['GET'])
    expect(wrapper.find('h1').text()).toBe('Welcome')
    expect(wrapper.find('iframe').attributes('srcdoc')).toBe('<p>Hello</p>')
    expect(wrapper.find('iframe').attributes('sandbox')).toBe('allow-same-origin')
  })

  it('links back to the email history', async () => {
    table[`GET ${EMAIL_PATH}`] = { body: { id: 1, to: 'a@b.test', subject: 'Welcome', sentAt: '2026-01-01T10:00:00Z', body: '', success: true } }

    const wrapper = await mountPage(EmailPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="email-back"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="email-back"]').attributes('href')).toBe('/client/emails')
  })

  it('says the email was not found when the read fails', async () => {
    table[`GET ${EMAIL_PATH}`] = { status: 404, body: { detail: 'Not found.' } }

    const wrapper = await mountPage(EmailPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.emails.notFound')
    expect(wrapper.find('iframe').exists()).toBe(false)
  })
})
