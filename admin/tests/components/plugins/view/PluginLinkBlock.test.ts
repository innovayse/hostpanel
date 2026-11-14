/**
 * Unit tests for PluginLinkBlock: a host-relative link stays in the panel, an https link on an allowed
 * origin opens in a new tab with the safe `rel`, and any other address renders nothing.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PluginBlockKind } from '@/types/pluginview'
import { json } from '../../../helpers/fetch'
import { ext, mountLoadingPluginView, mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('PluginLinkBlock', () => {
  it('links inside the panel for a host-relative address', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Link, label: 'Clients', href: '/clients' }])

    const link = wrapper.get('[data-testid="plugin-link"]')
    expect(link.attributes('href')).toBe('/clients')
    expect(link.attributes('target')).toBeUndefined()
  })

  it('draws a link as a button, with the external-link mark only when it leaves the panel', async () => {
    const { wrapper } = await mountPluginView([
      { kind: PluginBlockKind.Link, label: 'Clients', href: '/clients' },
      { kind: PluginBlockKind.Link, label: 'Docs', href: 'https://docs.example.com/a' },
    ], { externalOrigins: ['https://*.example.com'] })

    const [inside, outside] = wrapper.findAll('[data-testid="plugin-link"]')
    expect(inside?.classes()).toContain('cursor-pointer')
    expect(inside?.find('[data-testid="plugin-link-external"]').exists()).toBe(false)
    expect(outside?.find('[data-testid="plugin-link-external"]').exists()).toBe(true)
  })

  it('opens an allowed https origin in a new tab without the opener', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Link, label: 'Docs', href: 'https://docs.example.com/a' }], {
      externalOrigins: ['https://*.example.com'],
    })

    const link = wrapper.get('[data-testid="plugin-link"]')
    expect(link.attributes('href')).toBe('https://docs.example.com/a')
    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toBe('noopener noreferrer')
  })

  it('renders nothing for an origin the plugin did not declare, or a script address read from a route', async () => {
    const { wrapper } = await mountPluginView([
      { kind: PluginBlockKind.Link, label: 'Other', href: 'https://other.test/' },
      { kind: PluginBlockKind.Link, label: 'Read', source: { route: { method: 'get', path: '/site' }, field: 'url' } },
    ], {
      routes: { [`GET ${ext('site')}`]: () => json({ url: 'javascript:alert(1)' }) },
    })

    expect(wrapper.find('[data-testid="plugin-link"]').exists()).toBe(false)
  })

  it('draws a button-sized bar while the address is read', async () => {
    const wrapper = await mountLoadingPluginView([{ kind: PluginBlockKind.Link, label: 'Open', source: { route: { method: 'get', path: '/target' }, field: 'url' } }])

    expect(wrapper.get('[data-testid="plugin-link-loading"]').classes()).toContain('h-8')
    expect(wrapper.find('[data-testid="plugin-link"]').exists()).toBe(false)
  })
})
