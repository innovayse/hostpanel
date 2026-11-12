/**
 * Tests for PluginStatBlock: a value from the view, a value read from a GET route through the real store, a failed
 * read showing the API's sentence, a status word drawn as a badge, and escaping.
 *
 * @module tests/components/plugin/StatBlock.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, problem, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { PluginValueFormat } from '~/types/pluginvalueformat'
import { settle } from '~/tests/helpers/settle'

/** The stub API of the running test, closed after it. */
let api: StubServer | undefined

afterEach(async () => {
  await api?.close()
  api = undefined
  vi.unstubAllGlobals()
})

describe('PluginStatBlock', () => {
  it('shows a value written in the view, as text', () => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Stat, label: 'Pages', value: '<b>3</b>' }])

    const stat = wrapper.get('[data-testid="plugin-stat"]')
    expect(stat.text()).toContain('<b>3</b>')
    expect(stat.find('b').exists()).toBe(false)
  })

  it('draws the stat as a tile', () => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Stat, label: 'Pages', value: 3 }])

    const tile = wrapper.get('[data-testid="plugin-stat"]')
    expect(tile.classes()).toContain('rounded-2xl')
    expect(tile.get('[data-testid="ui-stat-tile-value"]').text()).toBe('3')
  })

  it('reads its value from the route, its parameters filled from the page', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { stats: { pages: 12 } } } })
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Stat, label: 'Pages', source: { route: TEST_ROUTES.site, field: 'stats.pages' } }])
    await settle()

    expect(wrapper.get('[data-testid="plugin-stat"]').text()).toContain('12')
    expect(api.requests.at(-1)?.headers['x-language']).toBe('en')
  })

  it('draws a status word read from the route as a badge, as text', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { statusText: '<i>Live</i>' } } })
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Stat, label: 'Status', format: PluginValueFormat.Badge, source: { route: TEST_ROUTES.site, field: 'statusText' },
    }])
    await settle()

    const badge = wrapper.get('[data-testid="plugin-stat-badge"]')
    expect(badge.text()).toBe('<i>Live</i>')
    expect(badge.find('i').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-stat"]').text()).toContain('Status')
  })

  it('shows a refused badge read as the API\'s sentence, not as a badge', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: problem(502, 'The plugin failed.') })
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Stat, label: 'Status', format: PluginValueFormat.Badge, source: { route: TEST_ROUTES.site, field: 'statusText' },
    }])
    await settle()

    expect(wrapper.find('[data-testid="plugin-stat-badge"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-stat"]').text()).toContain('The plugin failed.')
  })

  it('shows the API\'s sentence when the read is refused', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: problem(503, 'The site service is down.') })
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Stat, label: 'Pages', source: { route: TEST_ROUTES.site } }])
    await settle()

    expect(wrapper.get('[data-testid="plugin-stat"]').text()).toContain('The site service is down.')
  })
})
