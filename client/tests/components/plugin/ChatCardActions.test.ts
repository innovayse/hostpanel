/**
 * Tests for PluginChatCardActions: the row of a result card's actions is hidden while every action is hidden by its
 * condition and no details switch is offered, comes back when a refresh makes an action show, and is never hidden by
 * an action with no condition or by a details switch; a hidden action stays mounted so it can bring the row back.
 *
 * @module tests/components/plugin/ChatCardActions.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import { enableAutoUnmount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import PluginChatCardActions from '~/components/plugin/ChatCardActions.vue'
import { providePluginView } from '~/composables/usePluginView'
import { mountClient } from '~/tests/helpers/mountClient'
import { PLUGIN_COMPONENTS, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { settle } from '~/tests/helpers/settle'
import type { StubServer } from '~/tests/helpers/stubServer'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { PluginBlockTone } from '~/types/pluginblocktone'
import type { PluginChatResult, PluginChatResultAction } from '~/types/pluginchatresult'
import type { PluginViewCondition } from '~/types/pluginviewcondition'

// Every view shares the refresh counts below, so a view left mounted would read again in the next test.
enableAutoUnmount(afterEach)

/** The stub API of the running test, closed after it. */
let api: StubServer | undefined

/** The version the stub's state route reports as publishable. */
let publishVersion: number | null = 5

/** The refresh counts the test view hands out. */
const refreshes = ref<Record<string, number>>({})

/** The card under test, replaced per test. */
const result = ref<PluginChatResult>({ key: 0, tone: PluginBlockTone.Info, title: '', text: '', details: [], actions: [] })

/** How many details toggles the row reported. */
let toggles = 0

/** The condition the conditional actions carry: version 5 is still the publishable one. */
const STILL_FIVE: PluginViewCondition = { route: TEST_ROUTES.site, field: 'publishVersion', equals: [5] }

/** A call action that shows only while version 5 is the publishable one. */
const PUBLISH: PluginChatResultAction = {
  key: 0,
  label: 'Publish version 5',
  link: null,
  block: { kind: PluginBlockKind.Action, label: 'Publish version 5', route: TEST_ROUTES.publish },
  visibleWhen: STILL_FIVE,
}

/** A link action with no condition. */
const OPEN: PluginChatResultAction = { key: 1, label: 'Open', link: { href: '/client/services/7', external: false }, block: null }

/** A view holding the row, with the pieces of the view context the actions read. */
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
    return () => h(PluginChatCardActions, {
      result: result.value,
      refresh: ['top'],
      disabled: false,
      onToggleDetails: () => { toggles += 1 },
    })
  },
})

/**
 * Mounts the host.
 *
 * @returns The mounted view.
 */
const mountRow = (): VueWrapper => mountClient(Host, { components: PLUGIN_COMPONENTS })

/**
 * Whether the row is drawn on screen: present and not hidden by `v-show`.
 *
 * @param wrapper - The mounted host.
 * @returns True when the row shows.
 */
const rowShows = (wrapper: VueWrapper): boolean => {
  const row = wrapper.find('[data-testid="plugin-chat-result-actions"]')
  // Read from the style the directive sets: `isVisible` does not see it on a wrapper that is not in the document.
  return row.exists() && !(row.attributes('style') ?? '').includes('display: none')
}

/**
 * Asks the named block to read again, as an action or a finished turn does.
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
  toggles = 0
  refreshes.value = {}
  result.value = { key: 0, tone: PluginBlockTone.Info, title: '', text: '', details: [], actions: [PUBLISH] }
  api = await startPluginApi({
    [`GET ${ROUTES_URL}/sites/7`]: () => ({ body: { publishVersion } }),
  })
})

afterEach(async () => {
  await api?.close()
  api = undefined
  vi.unstubAllGlobals()
})

describe('PluginChatCardActions', () => {
  it('hides the row while its only action is hidden by the condition, and shows it once the condition holds', async () => {
    const wrapper = mountRow()
    expect(rowShows(wrapper)).toBe(false)
    await settle()

    expect(rowShows(wrapper)).toBe(true)
    expect(wrapper.get('[data-testid="plugin-action-button"]').text()).toBe('Publish version 5')
  })

  it('hides the row when every action is hidden because the condition no longer holds', async () => {
    publishVersion = 6
    const wrapper = mountRow()
    await settle()

    expect(rowShows(wrapper)).toBe(false)
  })

  it('hides the row when the only action stops holding, and brings it back when a refresh finds it holding again', async () => {
    const wrapper = mountRow()
    await settle()
    expect(rowShows(wrapper)).toBe(true)

    publishVersion = null
    await refresh('top')
    expect(rowShows(wrapper)).toBe(false)

    // The hidden action stayed mounted, so it reads again and the row returns.
    publishVersion = 5
    await refresh('top')
    expect(rowShows(wrapper)).toBe(true)
  })

  it('keeps the row while one action still shows', async () => {
    publishVersion = 6
    result.value = { ...result.value, actions: [PUBLISH, OPEN] }
    const wrapper = mountRow()
    expect(rowShows(wrapper)).toBe(true)
    await settle()

    expect(rowShows(wrapper)).toBe(true)
    expect(wrapper.find('[data-testid="plugin-action-button"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-chat-result-link"]').text()).toBe('Open')
  })

  it('keeps the row for the details switch when every action is hidden, and reports a press of it', async () => {
    publishVersion = 6
    result.value = { ...result.value, details: ['Built in 4 s'] }
    const wrapper = mountRow()
    await settle()

    expect(rowShows(wrapper)).toBe(true)
    expect(wrapper.find('[data-testid="plugin-action-button"]').exists()).toBe(false)
    await wrapper.get('[data-testid="plugin-chat-result-details-toggle"]').trigger('click')
    expect(toggles).toBe(1)
  })

  it('shows the row at once for an action with no condition, without waiting for a read', async () => {
    result.value = { ...result.value, actions: [OPEN] }
    const wrapper = mountRow()

    expect(rowShows(wrapper)).toBe(true)
    await settle()
    expect(api?.requests.filter(request => request.url.endsWith('/sites/7'))).toHaveLength(0)
  })
})
