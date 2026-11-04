/**
 * Tests for PluginTabsBlock: only the open tab's blocks are rendered, so a closed tab reads no route.
 *
 * @module tests/components/plugin/TabsBlock.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { settle } from '~/tests/helpers/settle'

/** The stub API of the running test, closed after it. */
let api: StubServer | undefined

afterEach(async () => {
  await api?.close()
  api = undefined
  // Cleared before the globals are unstubbed, which takes the setup's `navigateTo` stand-in away with them.
  vi.mocked(navigateTo).mockClear()
  vi.unstubAllGlobals()
})

describe('PluginTabsBlock', () => {
  it('renders the first tab, reads a closed tab only when opened', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { status: 'Live' } } })
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Tabs,
      tabs: [
        { id: 'overview', title: '<b>Overview</b>', blocks: [{ kind: PluginBlockKind.Text, text: 'First' }] },
        { id: 'status', title: 'Status', blocks: [{ kind: PluginBlockKind.Badge, source: { route: TEST_ROUTES.site, field: 'status' } }] },
      ],
    }])
    await settle()

    expect(wrapper.get('[data-testid="plugin-text"]').text()).toBe('First')
    expect(wrapper.get('[data-testid="ui-tab-overview"]').text()).toBe('<b>Overview</b>')
    expect(api.requests).toHaveLength(0)

    await wrapper.get('[data-testid="ui-tab-status"]').trigger('click')
    await settle()

    expect(wrapper.find('[data-testid="plugin-text"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('Live')
    expect(api.requests).toHaveLength(1)
  })
})
