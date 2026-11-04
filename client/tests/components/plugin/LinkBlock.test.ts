/**
 * Tests for PluginLinkBlock: only a host-relative path or an https address on an allowed origin is linked; an
 * external link opens in a new tab that cannot reach back; an address read from a route is checked the same way.
 *
 * @module tests/components/plugin/LinkBlock.test
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
  vi.unstubAllGlobals()
})

describe('PluginLinkBlock', () => {
  it('links an allowed https address in a new tab with noopener noreferrer', () => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Link, label: '<b>Preview</b>', href: 'https://blog.sites.example.test/' }])

    const link = wrapper.get('[data-testid="plugin-link"]')
    expect(link.attributes('href')).toBe('https://blog.sites.example.test/')
    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toContain('noopener')
    expect(link.attributes('rel')).toContain('noreferrer')
    expect(link.text()).toBe('<b>Preview</b>')
  })

  it('links a host-relative path inside the panel', () => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Link, label: 'Services', href: '/client/services/7' }])

    const link = wrapper.get('[data-testid="plugin-link"]')
    expect(link.attributes('href')).toBe('/client/services/7')
    expect(link.attributes('target')).toBeUndefined()
  })

  it.each([
    'https://evil.test/',
    'http://blog.sites.example.test/',
    'javascript:alert(1)',
    'data:text/html,<script>alert(1)</script>',
    '//evil.test/',
    '/\\evil.test',
    'https://user:pass@blog.sites.example.test/',
    'relative/path',
    '/client\n/x',
  ])('renders nothing for %s', (href) => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Link, label: 'Preview', href }])

    expect(wrapper.find('[data-testid="plugin-link"]').exists()).toBe(false)
  })

  it('links nothing absolute when the plugin declares no origins', () => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Link, label: 'Preview', href: 'https://blog.sites.example.test/' }], { externalOrigins: [] })

    expect(wrapper.find('[data-testid="plugin-link"]').exists()).toBe(false)
  })

  it('checks an address read from a route the same way', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { previewUrl: 'javascript:alert(1)', okUrl: 'https://x.sites.example.test/p' } } })
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Link, label: 'Bad', source: { route: TEST_ROUTES.site, field: 'previewUrl' } },
      { kind: PluginBlockKind.Link, label: 'Good', source: { route: TEST_ROUTES.site, field: 'okUrl' } },
    ])
    await settle()

    const links = wrapper.findAll('[data-testid="plugin-link"]')
    expect(links).toHaveLength(1)
    expect(links[0]!.attributes('href')).toBe('https://x.sites.example.test/p')
  })
})
