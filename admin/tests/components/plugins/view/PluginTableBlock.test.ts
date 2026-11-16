/**
 * Unit tests for PluginTableBlock: rows read from a route with formatted cells, paging in the panel, the
 * empty and failed states, cards display and a row link that opens a page of the index.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { usePluginUiStore } from '@/stores/pluginUi'
import { PluginBlockKind, PluginTableDisplay, PluginValueFormat } from '@/types/pluginview'
import type { PluginTableBlock } from '@/types/pluginview'
import { json, problem } from '../../../helpers/fetch'
import { ext, mountLoadingPluginView, mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

/**
 * Builds the table under test.
 *
 * @param overrides - Members to change.
 * @returns The table block.
 */
const table = (overrides: Partial<PluginTableBlock> = {}): PluginTableBlock => ({
  kind: PluginBlockKind.Table,
  title: 'Sites',
  source: { route: { method: 'get', path: '/sites' }, field: 'items' },
  columns: [
    { field: 'name', label: 'Name' },
    { field: 'state', label: 'State', format: PluginValueFormat.Badge },
  ],
  ...overrides,
})

/** Three sites as the route answers them. */
const sites = { items: [{ id: 1, name: 'Shop', state: 'live' }, { id: 2, name: 'Blog', state: 'draft' }, { id: 3, name: 'Docs', state: 'live' }] }

describe('PluginTableBlock', () => {
  it('draws one row per entry with its cells as text', async () => {
    const { wrapper } = await mountPluginView([table()], { routes: { [`GET ${ext('sites')}`]: () => json(sites) } })

    const rows = wrapper.findAll('[data-testid="plugin-table-row"]')
    expect(rows).toHaveLength(3)
    expect(rows[0]?.text()).toContain('Shop')
    expect(rows[0]?.text()).toContain('live')
  })

  it('pages the rows in the panel', async () => {
    const { wrapper } = await mountPluginView([table({ pageSize: 2 })], { routes: { [`GET ${ext('sites')}`]: () => json(sites) } })

    expect(wrapper.findAll('[data-testid="plugin-table-row"]')).toHaveLength(2)
    await wrapper.get('[data-testid="plugin-table-pagination-page-2"]').trigger('click')

    expect(wrapper.findAll('[data-testid="plugin-table-row"]').map((row) => row.text())).toEqual([expect.stringContaining('Docs')])
  })

  it('says so when there are no rows, and shows the API sentence when the read fails', async () => {
    const empty = await mountPluginView([table()], { routes: { [`GET ${ext('sites')}`]: () => json({ items: [] }) } })
    expect(empty.wrapper.get('[data-testid="plugin-table-empty"]').text()).toBe('Nothing to show yet.')

    const failed = await mountPluginView([table()], { routes: { [`GET ${ext('sites')}`]: () => problem('Sites are unavailable.', 502) } })
    expect(failed.wrapper.get('[data-testid="plugin-table-error"]').text()).toBe('Sites are unavailable.')
  })

  it('draws each row as a card on request', async () => {
    const { wrapper } = await mountPluginView([table({ display: PluginTableDisplay.Cards })], { routes: { [`GET ${ext('sites')}`]: () => json(sites) } })

    expect(wrapper.findAll('[data-testid="plugin-table-card"]')).toHaveLength(3)
  })

  it('opens the linked page of the index with the row values', async () => {
    const { wrapper } = await mountPluginView([table({ rowLink: { page: 'site', params: { siteId: 'id' } } })], {
      routes: {
        [`GET ${ext('sites')}`]: () => json(sites),
        'GET /admin/plugins/ui': () => json({ menus: [], pages: [{ pluginId: 'sites', id: 'site', path: '/sites/{siteId}', title: 'Site' }] }),
      },
    })
    await usePluginUiStore().loadUi()
    const router = wrapper.vm.$router

    await wrapper.findAll('[data-testid="plugin-table-row-open"]')[1]?.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.path).toBe('/apps/sites/sites/2')
  })

  it('keeps the header and shows skeleton rows while the source is read', async () => {
    const wrapper = await mountLoadingPluginView([table({ pageSize: 2 })])

    expect(wrapper.text()).toContain('Name')
    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-table-row"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-table-pagination-range"]').exists()).toBe(true)
  })

  it('draws placeholder cards of the real card layout while the rows of a cards display load', async () => {
    const wrapper = await mountLoadingPluginView([table({ display: PluginTableDisplay.Cards })])

    const cards = wrapper.get('[data-testid="plugin-table-cards"]')
    expect(cards.attributes('aria-busy')).toBe('true')
    expect(cards.findAll('[data-testid="plugin-table-card"]')).toHaveLength(4)
    expect(cards.findAll('[data-testid="ui-skeleton-bar"]').length).toBe(12)
  })
})
