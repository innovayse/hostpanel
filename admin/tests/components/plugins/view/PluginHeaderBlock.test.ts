/**
 * Unit tests for PluginHeaderBlock: the title written or read from a route, the compact line with its
 * smaller line, the actions beside it and the one sentence line under the whole header.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { PluginBlockKind } from '@/types/pluginview'
import { json } from '../../../helpers/fetch'
import { ext, mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('PluginHeaderBlock', () => {
  it('shows the written title with its actions, and an action sentence under the whole header', async () => {
    const { wrapper } = await mountPluginView([
      { kind: PluginBlockKind.Header, title: 'Sites', actions: [{ kind: PluginBlockKind.Action, label: 'Sync', route: { method: 'post', path: '/sync' } }] },
    ], {
      routes: { [`POST ${ext('sync')}`]: () => json({ message: 'Synced.' }) },
    })

    expect(wrapper.get('[data-testid="plugin-header"]').text()).toContain('Sites')
    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-action-message"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-header-message"]').text()).toBe('Synced.')
  })

  it('reads a compact header title and its smaller line from routes', async () => {
    const { wrapper } = await mountPluginView([
      {
        kind: PluginBlockKind.Header,
        compact: true,
        title: 'Site',
        titleSource: { route: { method: 'get', path: '/site' }, field: 'name' },
        subtitleSource: { route: { method: 'get', path: '/site' }, field: 'domain' },
      },
    ], {
      routes: { [`GET ${ext('site')}`]: () => json({ name: 'Shop', domain: 'shop.example.com' }) },
    })

    expect(wrapper.find('[data-testid="plugin-header-compact"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-header"]').text()).toBe('Shop')
    expect(wrapper.get('[data-testid="plugin-header-subtitle"]').text()).toBe('shop.example.com')
  })
})
