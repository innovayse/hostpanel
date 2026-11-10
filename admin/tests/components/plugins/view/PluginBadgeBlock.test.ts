/**
 * Unit tests for PluginBadgeBlock: a written word, a word read from a route, a failed read and a value
 * that is no word.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PluginBlockKind, PluginBlockTone } from '@/types/pluginview'
import { json, problem } from '../../../helpers/fetch'
import { ext, mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** The route the badge reads. */
const status = { route: { method: 'get', path: '/status' }, field: 'state' }

describe('PluginBadgeBlock', () => {
  it('shows the word the view writes in its tone', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Badge, text: 'Live', tone: PluginBlockTone.Success }])

    const badge = wrapper.get('[data-testid="plugin-badge"]')
    expect(badge.text()).toBe('Live')
    expect(badge.classes()).toContain('text-status-green')
  })

  it('shows the word read from the route', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Badge, source: status }], {
      routes: { [`GET ${ext('status')}`]: () => json({ state: 'building' }) },
    })

    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('building')
  })

  it('shows the API sentence of a failed read', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Badge, source: status }], {
      routes: { [`GET ${ext('status')}`]: () => problem('Plugin is down.', 502) },
    })

    expect(wrapper.get('[data-testid="plugin-badge-error"]').text()).toBe('Plugin is down.')
  })

  it('draws nothing for an object, never its JSON', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Badge, source: status }], {
      routes: { [`GET ${ext('status')}`]: () => json({ state: { nested: true } }) },
    })

    expect(wrapper.find('[data-testid="plugin-badge"]').exists()).toBe(false)
  })
})
