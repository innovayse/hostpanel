/**
 * Tests for PluginChatCardAction: an action with no condition always shows and reads nothing; one with a condition is
 * hidden while the condition is read, when the read is refused and when it does not hold, shown once it holds, and
 * hidden again when a refresh of one of the chat's blocks finds that it stopped holding; a refresh of another block
 * reads nothing; the condition applies to a call, a link and a "try again" alike.
 *
 * @module tests/components/plugin/ChatCardAction.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import { enableAutoUnmount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import PluginChatCardAction from '~/components/plugin/ChatCardAction.vue'
import { providePluginView } from '~/composables/usePluginView'
import { mountClient } from '~/tests/helpers/mountClient'
import { PLUGIN_COMPONENTS, problem, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { settle } from '~/tests/helpers/settle'
import type { StubServer } from '~/tests/helpers/stubServer'
import { PluginBlockKind } from '~/types/pluginblockkind'
import type { PluginChatResultAction } from '~/types/pluginchatresult'
import type { PluginViewCondition } from '~/types/pluginviewcondition'

// Every view shares the refresh counts below, so a view left mounted would read again in the next test.
enableAutoUnmount(afterEach)

/** The stub API of the running test, closed after it. */
let api: StubServer | undefined

/** The version the stub's state route reports as publishable; null for none. */
let publishVersion: number | null = 5

/** The refresh counts the test view hands out. */
const refreshes = ref<Record<string, number>>({})

/** The action under test, replaced per test. */
const action = ref<PluginChatResultAction>({ key: 0, label: 'Publish', link: null, block: null })

/** The block ids the card's chat reads again. */
const chatRefresh = ref<string[]>(['top'])

/** How many "try again" requests the card made. */
let resends = 0

/** The condition the cards below carry: version 5 is still the publishable one. */
const STILL_FIVE: PluginViewCondition = { route: TEST_ROUTES.site, field: 'publishVersion', equals: [5] }

/** A view holding the card action, with the pieces of the view context the action reads. */
const Host = defineComponent({
  setup() {
    providePluginView({
      pluginId: 'acme-sites',
      values: { siteId: '7' },
      externalOrigins: [],
      frameOrigins: [],
      refreshes,
      refresh: () => undefined,
      open: () => undefined,
      adopt: () => () => undefined,
      track: () => undefined,
    })
    return () => h(PluginChatCardAction, { action: action.value, refresh: chatRefresh.value, onResend: () => { resends += 1 } })
  },
})

/**
 * Mounts the host.
 *
 * @returns The mounted view.
 */
const mountAction = (): VueWrapper => mountClient(Host, { components: PLUGIN_COMPONENTS })

/**
 * Counts the reads of the state route so far.
 *
 * @returns The count.
 */
const reads = (): number => api?.requests.filter(request => request.method === 'GET' && request.url.endsWith('/sites/7')).length ?? 0

/**
 * Asks the named blocks to read again, as an action or a finished turn does.
 *
 * @param id - The block id.
 * @returns Resolves once the reads it started have answered.
 */
const refresh = async (id: string): Promise<void> => {
  refreshes.value = { ...refreshes.value, [id]: (refreshes.value[id] ?? 0) + 1 }
  await nextTick()
  await settle()
}

beforeEach(async () => {
  publishVersion = 5
  resends = 0
  refreshes.value = {}
  chatRefresh.value = ['top']
  action.value = {
    key: 0,
    label: 'Publish version 5',
    link: null,
    block: { kind: PluginBlockKind.Action, label: 'Publish version 5', route: TEST_ROUTES.publish },
    visibleWhen: STILL_FIVE,
  }
  api = await startPluginApi({
    [`GET ${ROUTES_URL}/sites/7`]: () => ({ body: { publishVersion } }),
  })
})

afterEach(async () => {
  await api?.close()
  api = undefined
  vi.unstubAllGlobals()
})

describe('PluginChatCardAction', () => {
  it('shows an action with no condition and reads nothing', async () => {
    action.value = { key: 0, label: 'Open', link: { href: '/client/services/7', external: false }, block: null }
    const wrapper = mountAction()
    await settle()

    expect(wrapper.get('[data-testid="plugin-chat-result-link"]').text()).toBe('Open')
    expect(reads()).toBe(0)
  })

  it('hides the action while its condition is read, then shows it once it holds', async () => {
    const wrapper = mountAction()
    expect(wrapper.find('[data-testid="plugin-action-button"]').exists()).toBe(false)
    await settle()

    expect(wrapper.get('[data-testid="plugin-action-button"]').text()).toBe('Publish version 5')
    expect(reads()).toBe(1)
  })

  it('hides the action when another version is the publishable one, or none is', async () => {
    publishVersion = 6
    const superseded = mountAction()
    await settle()
    expect(superseded.find('[data-testid="plugin-action-button"]').exists()).toBe(false)

    publishVersion = null
    const published = mountAction()
    await settle()
    expect(published.find('[data-testid="plugin-action-button"]').exists()).toBe(false)
  })

  it('hides the action when the read is refused', async () => {
    await api?.close()
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: problem(502, 'The plugin failed.') })
    const wrapper = mountAction()
    await settle()

    expect(wrapper.find('[data-testid="plugin-action-button"]').exists()).toBe(false)
  })

  it('hides the action again when a refresh of a block the chat reads finds it no longer holds', async () => {
    const wrapper = mountAction()
    await settle()
    expect(wrapper.find('[data-testid="plugin-action-button"]').exists()).toBe(true)

    publishVersion = 6
    await refresh('top')

    expect(wrapper.find('[data-testid="plugin-action-button"]').exists()).toBe(false)
    expect(reads()).toBe(2)
  })

  it('reads nothing when a block the chat does not read is refreshed', async () => {
    const wrapper = mountAction()
    await settle()

    publishVersion = 6
    await refresh('elsewhere')

    expect(wrapper.find('[data-testid="plugin-action-button"]').exists()).toBe(true)
    expect(reads()).toBe(1)
  })

  it('applies the condition to a link and to a "try again" as well', async () => {
    publishVersion = 6
    action.value = { key: 0, label: 'Open', link: { href: '/client/services/7', external: false }, block: null, visibleWhen: STILL_FIVE }
    const link = mountAction()
    await settle()
    expect(link.find('[data-testid="plugin-chat-result-link"]').exists()).toBe(false)

    action.value = { key: 0, label: 'Try again', link: null, block: null, resend: true, visibleWhen: STILL_FIVE }
    const retry = mountAction()
    await settle()
    expect(retry.find('[data-testid="plugin-chat-result-resend"]').exists()).toBe(false)

    publishVersion = 5
    await refresh('top')
    await retry.get('[data-testid="plugin-chat-result-resend"]').trigger('click')
    expect(resends).toBe(1)
  })
})
