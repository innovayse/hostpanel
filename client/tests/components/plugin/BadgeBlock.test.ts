/**
 * Tests for PluginBadgeBlock: a word from the view or a route, an object refused as a word, a failed read.
 *
 * @module tests/components/plugin/BadgeBlock.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, problem, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { settle } from '~/tests/helpers/settle'

/** The stub API of the running test, closed after it. */
let api: StubServer | undefined

afterEach(async () => {
  await api?.close()
  api = undefined
  vi.unstubAllGlobals()
})

describe('PluginBadgeBlock', () => {
  it('shows the view\'s word as text', () => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Badge, text: '<u>Live</u>' }])

    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('<u>Live</u>')
    expect(wrapper.find('[data-testid="plugin-badge"] u').exists()).toBe(false)
  })

  it('reads the word from the route', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { status: 'Published' } } })
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Badge, source: { route: TEST_ROUTES.site, field: 'status' } }])
    await settle()

    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('Published')
  })

  it('draws nothing for an object, which is not a word', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { status: { a: 1 } } } })
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Badge, source: { route: TEST_ROUTES.site, field: 'status' } }])
    await settle()

    expect(wrapper.find('[data-testid="plugin-badge"]').exists()).toBe(false)
  })

  it('shows the API\'s sentence when the read fails', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: problem(502, 'The plugin failed.') })
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Badge, source: { route: TEST_ROUTES.site, field: 'status' } }])
    await settle()

    expect(wrapper.get('[data-testid="plugin-badge-error"]').text()).toBe('The plugin failed.')
  })
})
