/**
 * Unit tests for PluginView and PluginBlock: every block in order, nothing for a kind the admin panel
 * does not draw (a preview, an unknown kind), `visibleWhen`, and the reads of one route shared by the
 * blocks of a view.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import PluginView from '@/components/plugins/view/PluginView.vue'
import { PluginBlockKind } from '@/types/pluginview'
import type { PluginBlock } from '@/types/pluginview'
import { json } from '../../../helpers/fetch'
import { requestsOf } from '../../../helpers/routes'
import { ext, mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** The route the shared reads name. */
const state = { method: 'get', path: '/state' }

describe('PluginView', () => {
  it('draws the blocks in order and nothing for a preview or an unknown kind', async () => {
    const { wrapper } = await mountPluginView([
      { kind: PluginBlockKind.Text, text: 'One' },
      { kind: PluginBlockKind.Preview },
      { kind: 'future-kind' } as unknown as PluginBlock,
      { kind: PluginBlockKind.Text, text: 'Two' },
    ])

    expect(wrapper.findComponent(PluginView).exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="plugin-text"]').map((text) => text.text())).toEqual(['One', 'Two'])
    expect(wrapper.get('[data-testid="plugin-view"]').element.children).toHaveLength(2)
  })

  it('shows a block only while its condition holds', async () => {
    const { wrapper } = await mountPluginView([
      { kind: PluginBlockKind.Text, text: 'Shown', visibleWhen: { route: state, field: 'site.exists', equals: true } },
      { kind: PluginBlockKind.Text, text: 'Hidden', visibleWhen: { route: state, field: 'site.exists', equals: [false, null] } },
      { kind: PluginBlockKind.Text, text: 'Missing field', visibleWhen: { route: state, field: 'site.none', equals: null } },
    ], {
      routes: { [`GET ${ext('state')}`]: () => json({ site: { exists: true } }) },
    })

    expect(wrapper.findAll('[data-testid="plugin-text"]').map((text) => text.text())).toEqual(['Shown'])
  })

  it('reads one route once for every block of the view that names it', async () => {
    const { fetch } = await mountPluginView([
      { kind: PluginBlockKind.Badge, source: { route: state, field: 'status' } },
      { kind: PluginBlockKind.Stat, label: 'Version', source: { route: state, field: 'version' } },
      { kind: PluginBlockKind.Text, text: 'Live', visibleWhen: { route: state, field: 'status', equals: 'live' } },
    ], {
      routes: { [`GET ${ext('state')}`]: () => json({ status: 'live', version: 3 }) },
    })

    expect(requestsOf(fetch)).toEqual([`GET ${ext('state')}`])
  })
})
