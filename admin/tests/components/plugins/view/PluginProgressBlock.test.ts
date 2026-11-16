/**
 * Unit tests for PluginProgressBlock: a share of `max`, clamped to the bar, read from the view or a route.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PluginBlockKind } from '@/types/pluginview'
import { json } from '../../../helpers/fetch'
import { ext, mountLoadingPluginView, mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('PluginProgressBlock', () => {
  it('shows the written value as a share of max', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Progress, label: 'Storage', value: 5, max: 20 }])

    expect(wrapper.get('[data-testid="ui-progress-value"]').text()).toBe('25%')
    expect(wrapper.get('progress').attributes('value')).toBe('25')
  })

  it('clamps a value read from the route to the full bar', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Progress, label: 'Build', source: { route: { method: 'get', path: '/build' }, field: 'done' } }], {
      routes: { [`GET ${ext('build')}`]: () => json({ done: 250 }) },
    })

    expect(wrapper.get('[data-testid="ui-progress-value"]').text()).toBe('100%')
  })

  it('draws the progress placeholder with its label and track while the value is read', async () => {
    const wrapper = await mountLoadingPluginView([{ kind: PluginBlockKind.Progress, label: 'Build', source: { route: { method: 'get', path: '/build' }, field: 'done' } }])

    expect(wrapper.get('[data-testid="plugin-progress"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.find('[data-testid="ui-progress-value"]').exists()).toBe(false)
  })
})
