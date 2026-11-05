/**
 * Tests for PluginBlock: each known kind draws its component, an unknown kind draws nothing, and a block with
 * `visibleWhen` shows only while its condition holds, read again when its id is refreshed.
 *
 * @module tests/components/plugin/Block.test
 */

import { afterEach, describe, expect, it } from 'vitest'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { settle } from '~/tests/helpers/settle'
import type { PluginBlock } from '~/types/pluginblock'
import { PluginBlockKind } from '~/types/pluginblockkind'

/** The stub API of the running test, closed after it. */
let api: StubServer | undefined

afterEach(async () => {
  await api?.close()
  api = undefined
})

describe('PluginBlock', () => {
  it('draws a known kind with its component', () => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Text, text: 'Hello' }])

    expect(wrapper.find('[data-testid="plugin-text"]').exists()).toBe(true)
  })

  it.each(['iframe', 'script', '__proto__', 'toString'])('draws nothing for the unknown kind %s', (kind) => {
    const wrapper = mountBlocks([{ kind, text: 'x' } as unknown as PluginBlock, { kind: PluginBlockKind.Text, text: 'after' }])

    expect(wrapper.findAll('[data-testid="plugin-view"] > *')).toHaveLength(1)
    expect(wrapper.get('[data-testid="plugin-text"]').text()).toBe('after')
  })

  it('shows a block only while its condition holds, and again after an action refreshes its id', async () => {
    let status = 'none'
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: () => ({ body: { site: { status } } }),
      [`POST ${ROUTES_URL}/sites/7/publish`]: () => {
        status = 'live'
        return { status: 204 }
      },
    })
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Text, id: 'done', text: 'Published', visibleWhen: { route: TEST_ROUTES.site, field: 'site.status', equals: ['ready', 'live'] } },
      { kind: PluginBlockKind.Text, id: 'create', text: 'Create', visibleWhen: { route: TEST_ROUTES.site, field: 'site.status', equals: 'none' } },
      { kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish, refresh: ['done', 'create'] },
    ])
    expect(wrapper.findAll('[data-testid="plugin-text"]')).toHaveLength(0)
    await settle()
    expect(wrapper.findAll('[data-testid="plugin-text"]').map(text => text.text())).toEqual(['Create'])

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    expect(wrapper.findAll('[data-testid="plugin-text"]').map(text => text.text())).toEqual(['Published'])
  })

  it('hides a block whose condition cannot be read', async () => {
    api = await startPluginApi({})
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Text, text: 'Secret', visibleWhen: { route: TEST_ROUTES.site, field: 'ok', equals: true } },
    ])
    await settle()

    expect(wrapper.find('[data-testid="plugin-text"]').exists()).toBe(false)
  })
})
