/**
 * Tests for PluginView: blocks in document order, and the context the blocks share (values fill route parameters).
 *
 * @module tests/components/plugin/View.test
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

describe('PluginView', () => {
  it('renders the blocks in document order', () => {
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Text, text: 'one' },
      { kind: PluginBlockKind.Text, text: 'two' },
    ])

    expect(wrapper.findAll('[data-testid="plugin-text"]').map(t => t.text())).toEqual(['one', 'two'])
  })

  it('fills route parameters from its values, percent-encoded', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/a%2F..`]: { body: { status: 'ok' } } })
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Badge, source: { route: TEST_ROUTES.site, field: 'status' } }], { values: { siteId: 'a/..' } })
    await settle()

    expect(api.requests.at(-1)?.url).toBe(`${ROUTES_URL}/sites/a%2F..`)
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('ok')
  })
})
