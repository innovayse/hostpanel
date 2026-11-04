/**
 * Tests for PluginTableBlock: rows from a GET route as escaped text, the empty and failed states, paging, and a row
 * link opening a page of the index.
 *
 * @module tests/components/plugin/TableBlock.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, problem, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
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

/** The pages table of the worked example. */
const table = {
  kind: PluginBlockKind.Table,
  title: 'Pages',
  source: { route: TEST_ROUTES.site, field: 'pages' },
  columns: [{ field: 'title', label: 'Title' }, { field: 'state', label: 'State' }],
} as const

describe('PluginTableBlock', () => {
  it('shows each row\'s fields as text', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { pages: [{ id: 1, title: '<script>x</script>', state: 'draft' }] } } })
    const wrapper = mountBlocks([table])
    await settle()

    const row = wrapper.get('[data-testid="plugin-table-row"]')
    expect(row.text()).toContain('<script>x</script>')
    expect(row.find('script').exists()).toBe(false)
  })

  it('says so when there are no rows, or the reply is not a list', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { pages: 'nope' } } })
    const wrapper = mountBlocks([table])
    await settle()

    expect(wrapper.get('[data-testid="plugin-table-empty"]').text()).toBe('plugins.tableEmpty')
  })

  it('shows the API\'s sentence when the read fails', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: problem(502, 'The plugin failed.') })
    const wrapper = mountBlocks([table])
    await settle()

    expect(wrapper.get('[data-testid="plugin-table-error"]').text()).toBe('The plugin failed.')
  })

  it('pages the rows in the panel', async () => {
    const pages = Array.from({ length: 5 }, (_, i) => ({ id: i, title: `P${i}`, state: 'ok' }))
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { pages } } })
    const wrapper = mountBlocks([{ ...table, pageSize: 2 }])
    await settle()

    expect(wrapper.findAll('[data-testid="plugin-table-row"]')).toHaveLength(2)
    await wrapper.get('[data-testid="ui-pagination-page-3"]').trigger('click')
    expect(wrapper.findAll('[data-testid="plugin-table-row"]')).toHaveLength(1)
  })

  it('opens the linked page with parameters from the row', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { pages: [{ id: 9, title: 'Home', state: 'ok' }] } } })
    const wrapper = mountBlocks([{ ...table, rowLink: { page: 'site', params: { siteId: 'id' } } }])
    await settle()

    await wrapper.get('[data-testid="plugin-table-row-open"]').trigger('click')

    expect(navigateTo).toHaveBeenCalledWith('/client/apps/acme-sites/sites/9')
  })
})
