/**
 * Tests for `pages/unsubscribe.vue` against the real unsubscribe store, the real API composable and
 * the real transport, with a stub BFF answering on a real socket: the panels the attempt walks through.
 *
 * @module tests/pages/unsubscribe.test
 */

import { flushPromises } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAuthPage, stubAuthRuntime } from '~/tests/helpers/authScreens'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { settle } from '~/tests/helpers/settle'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The `useSeoMeta` spy, to read the robots tag the page sets. */
const useSeoMeta = vi.fn()

const { default: UnsubscribePage } = await import('~/pages/unsubscribe.vue')

/** The endpoint the page unsubscribes through. */
const UNSUBSCRIBE_PATH = '/api/portal/public/unsubscribe'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

beforeEach(async () => {
  useSeoMeta.mockReset()
  vi.stubGlobal('useSeoMeta', useSeoMeta)
  table = { [`POST ${UNSUBSCRIBE_PATH}`]: { status: 204 } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

/** The panels the page ends on, by the test id that marks each. */
enum OutcomePanel {
  /** The reader is unsubscribed. */
  Done = 'unsubscribe-done',
  /** The API refused the link. */
  Failed = 'unsubscribe-failed'
}

/**
 * Mounts the page and presses the confirm button, as the reader does.
 *
 * @param panel - The panel the reply must end on.
 * @returns The wrapper once the one confirm request has reached the stub, its panel is shown and the event loop has settled, so a late extra request would already be counted.
 */
const mountAndConfirm = async (panel: OutcomePanel) => {
  const wrapper = await mountAuthPage(UnsubscribePage)
  await wrapper.find('[data-testid="unsubscribe-confirm-button"]').trigger('click')
  await waitUntil(() => expect(requestsTo(stub.requests, UNSUBSCRIBE_PATH)).toHaveLength(1))
  await waitUntil(() => expect(wrapper.find(`[data-testid="${panel}"]`).exists()).toBe(true))
  await settle()
  return wrapper
}

describe('unsubscribe page', () => {
  it('sends nothing when it opens, only asks for confirmation', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })

    const wrapper = await mountAuthPage(UnsubscribePage)
    await flushPromises()

    expect(requestsTo(stub.requests, UNSUBSCRIBE_PATH)).toHaveLength(0)
    expect(wrapper.find('[data-testid="unsubscribe-confirm"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="unsubscribe-done"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="unsubscribe-failed"]').exists()).toBe(false)
  })

  it('sends the token of the link once the reader confirms, then shows the done panel', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })

    const wrapper = await mountAndConfirm(OutcomePanel.Done)

    const sent = requestsTo(stub.requests, UNSUBSCRIBE_PATH)
    expect(sent).toHaveLength(1)
    expect(sent[0]?.method).toBe('POST')
    expect(bodyOf(sent[0])).toEqual({ token: 'tok' })
    expect(wrapper.find('[data-testid="unsubscribe-done"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="unsubscribe-confirm"]').exists()).toBe(false)
  })

  it('shows the API sentence when the link is refused', async () => {
    stubAuthRuntime({ query: { token: 'bad' } })
    table[`POST ${UNSUBSCRIBE_PATH}`] = { status: 400, body: { detail: 'This link is not valid.' } }

    const wrapper = await mountAndConfirm(OutcomePanel.Failed)

    expect(wrapper.find('[data-testid="unsubscribe-failed"]').text()).toContain('This link is not valid.')
    expect(wrapper.find('[data-testid="unsubscribe-done"]').exists()).toBe(false)
  })

  it('asks the API about a link without a token, so its sentence is the one shown', async () => {
    stubAuthRuntime({ query: {} })
    table[`POST ${UNSUBSCRIBE_PATH}`] = { status: 400, body: { detail: 'A token is required.' } }

    const wrapper = await mountAndConfirm(OutcomePanel.Failed)

    const [sent] = requestsTo(stub.requests, UNSUBSCRIBE_PATH)
    expect(bodyOf(sent)).toEqual({ token: '' })
    expect(wrapper.find('[data-testid="unsubscribe-failed"]').text()).toContain('A token is required.')
  })

  it('disables the button and marks it busy while the request is open', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })
    table[`POST ${UNSUBSCRIBE_PATH}`] = { hold: true }

    const wrapper = await mountAuthPage(UnsubscribePage)
    await wrapper.find('[data-testid="unsubscribe-confirm-button"]').trigger('click')
    await waitUntil(() => expect(requestsTo(stub.requests, UNSUBSCRIBE_PATH)).toHaveLength(1))

    const button = wrapper.find('[data-testid="unsubscribe-confirm-button"]')
    expect(button.attributes('disabled')).toBeDefined()
    expect(button.attributes('aria-busy')).toBe('true')
  })

  it('keeps itself out of search results', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })

    await mountAuthPage(UnsubscribePage)

    expect(useSeoMeta).toHaveBeenCalledWith({ robots: 'noindex, nofollow' })
  })
})
