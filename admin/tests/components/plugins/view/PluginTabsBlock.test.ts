/**
 * Unit tests for PluginTabsBlock: only the open tab's blocks render and read, and switching tabs mounts
 * the other tab's blocks.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PluginBlockKind } from '@/types/pluginview'
import { json } from '../../../helpers/fetch'
import { requestsOf } from '../../../helpers/routes'
import { ext, mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('PluginTabsBlock', () => {
  it('reads only the open tab and mounts the other one when it opens', async () => {
    const { wrapper, fetch } = await mountPluginView([
      {
        kind: PluginBlockKind.Tabs,
        tabs: [
          { id: 'overview', title: 'Overview', blocks: [{ kind: PluginBlockKind.Text, text: 'First tab' }] },
          { id: 'usage', title: 'Usage', blocks: [{ kind: PluginBlockKind.Stat, label: 'Visits', source: { route: { method: 'get', path: '/usage' }, field: 'visits' } }] },
        ],
      },
    ], {
      routes: { [`GET ${ext('usage')}`]: () => json({ visits: 42 }) },
    })

    expect(wrapper.get('[data-testid="plugin-text"]').text()).toBe('First tab')
    expect(fetch).not.toHaveBeenCalled()

    await wrapper.get('[data-testid="plugin-tab-usage"]').trigger('click')
    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="plugin-stat"]').text()).toContain('42')
    })

    expect(wrapper.find('[data-testid="plugin-text"]').exists()).toBe(false)
    expect(requestsOf(fetch)).toEqual([`GET ${ext('usage')}`])
  })
})
