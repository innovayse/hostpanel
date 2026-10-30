/**
 * Smoke tests for TldConfigsView: the table with prices and margin, search and filters, paging,
 * import and sync with their result banner, and the delete confirmation, at network level.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import TldConfigsView from '@/views/TldConfigsView.vue'
import type { TldConfigListItem } from '@/types/tldconfig'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { pickOption } from '../helpers/select'
import { requestsOf, stubRouteTable } from '../helpers/routes'

/**
 * Builds a list item.
 *
 * @param tld - The extension.
 * @param extra - Fields to override.
 * @returns The list item.
 */
const item = (tld: string, extra: Partial<TldConfigListItem> = {}): TldConfigListItem => ({
  id: 1,
  tld,
  registrarModule: 'NameAm',
  isEnabled: true,
  costRegister1yr: 10,
  sellRegister1yr: 12.5,
  marginPercent: 25,
  currency: 'USD',
  sellCurrency: 'AMD',
  categories: ['popular'],
  lastSyncedAt: null,
  sortOrder: 0,
  ...extra,
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('TldConfigsView', () => {
  it('shows a row per TLD with prices in their symbols, margin, categories and sync state', async () => {
    stubRouteTable({ 'GET /admin/tld-configs': () => json([item('com'), item('net', { id: 2, marginPercent: -5, isEnabled: false, categories: [] })]) })
    const { wrapper } = await mountScreen(TldConfigsView)
    const com = wrapper.get('[data-testid="tld-row-com"]').text()

    expect(wrapper.get('h1').text()).toBe('TLD Pricing')
    expect(com).toContain('.com')
    expect(com).toContain('$10.00')
    expect(com).toContain('֏12.50')
    expect(com).toContain('+25.0%')
    expect(com).toContain('popular')
    expect(com).toContain('Never')
    expect(wrapper.get('[data-testid="tld-row-net"]').text()).toContain('-5.0%')
    expect(wrapper.get('[data-testid="tld-row-net"]').text()).toContain('Disabled')
  })

  it('searches by extension and filters by status', async () => {
    stubRouteTable({ 'GET /admin/tld-configs': () => json([item('com'), item('org', { id: 2, isEnabled: false })]) })
    const { wrapper } = await mountScreen(TldConfigsView)

    await wrapper.get('[data-testid="tld-configs-search"]').setValue('or')
    expect(wrapper.find('[data-testid="tld-row-com"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="tld-row-org"]').exists()).toBe(true)

    await wrapper.get('[data-testid="tld-configs-search"]').setValue('')
    await pickOption(wrapper, 'tld-configs-status-filter', 'Enabled')
    expect(wrapper.find('[data-testid="tld-row-com"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tld-row-org"]').exists()).toBe(false)
  })

  it('pages a long list', async () => {
    const many = Array.from({ length: 25 }, (_, index) => item(`t${String(index).padStart(2, '0')}`, { id: index + 1 }))
    stubRouteTable({ 'GET /admin/tld-configs': () => json(many) })
    const { wrapper } = await mountScreen(TldConfigsView)

    expect(wrapper.findAll('[data-testid^="tld-row-"]')).toHaveLength(20)
    expect(wrapper.text()).toContain('Showing 1–20 of 25 TLDs')

    await wrapper.get('[data-testid="tld-configs-pagination-next"]').trigger('click')
    expect(wrapper.findAll('[data-testid^="tld-row-"]')).toHaveLength(5)
  })

  it('imports from a provider chosen in the menu and reports the counts', async () => {
    const mock = stubRouteTable({
      'GET /admin/tld-configs': () => json([item('com')]),
      'POST /admin/tld-configs/import/Namecheap': () => json({ imported: 2, updated: 1 }),
    })
    const { wrapper } = await mountScreen(TldConfigsView)

    await pickOption(wrapper, 'tld-configs-import', 'Namecheap')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tld-configs-result"]').text()).toBe('Import complete: 2 imported, 1 updated.')
    })
    expect(requestsOf(mock)).toContain('POST /admin/tld-configs/import/Namecheap')
  })

  it('syncs prices and shows a refusal in the banner', async () => {
    stubRouteTable({
      'GET /admin/tld-configs': () => json([item('com')]),
      'POST /admin/tld-configs/sync': () => problem('The registrar did not answer.', 502),
    })
    const { wrapper } = await mountScreen(TldConfigsView)

    await wrapper.get('[data-testid="tld-configs-sync"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tld-configs-result"]').text()).toBe('The registrar did not answer.')
    })
  })

  it('asks before deleting a TLD, then deletes it', async () => {
    const id = item('com').id
    const mock = stubRouteTable({
      'GET /admin/tld-configs': () => json([item('com')]),
      [`DELETE /admin/tld-configs/${id}`]: () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(TldConfigsView)

    await wrapper.get('[data-testid="tld-row-com"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="tld-delete-com"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Delete ".com" TLD configuration?')
    expect(requestsOf(mock)).not.toContain(`DELETE /admin/tld-configs/${id}`)

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain(`DELETE /admin/tld-configs/${id}`)
    })
    await vi.waitFor(() => {
      expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    })
    expect(wrapper.get('[data-testid="tld-configs-result"]').text()).toBe('Record deleted.')
  })

  it('shows the API sentence when a delete is refused, and sends nothing when dismissed', async () => {
    const id = item('com').id
    const mock = stubRouteTable({
      'GET /admin/tld-configs': () => json([item('com')]),
      [`DELETE /admin/tld-configs/${id}`]: () => problem('The TLD has domains.', 409),
    })
    const { wrapper } = await mountScreen(TldConfigsView)

    await wrapper.get('[data-testid="tld-row-com"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="tld-delete-com"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    expect(requestsOf(mock)).not.toContain(`DELETE /admin/tld-configs/${id}`)

    await wrapper.get('[data-testid="tld-row-com"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="tld-delete-com"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tld-configs-result"]').text()).toBe('The TLD has domains.')
    })
  })

  it('navigates to the create and edit pages', async () => {
    stubRouteTable({ 'GET /admin/tld-configs': () => json([item('com')]) })
    const { wrapper, router } = await mountScreen(TldConfigsView)

    await wrapper.get('[data-testid="tld-configs-add"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/settings/tld-pricing/create')
    })

    await wrapper.get('[data-testid="tld-row-com"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="tld-edit-com"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe(`/settings/tld-pricing/${item('com').id}/edit`)
    })
  })

  it('says so when nothing matches and shows a load refusal', async () => {
    stubRouteTable({ 'GET /admin/tld-configs': () => json([]) })
    const empty = await mountScreen(TldConfigsView)
    expect(empty.wrapper.text()).toContain('No TLD configurations found')

    stubRouteTable({ 'GET /admin/tld-configs': () => problem('Pricing is unavailable.', 503) })
    const failed = await mountScreen(TldConfigsView)
    expect(failed.wrapper.get('[data-testid="tld-configs-error"]').text()).toBe('Pricing is unavailable.')
  })

  it('loads the list again from the retry of a refused load', async () => {
    let calls = 0
    stubRouteTable({
      'GET /admin/tld-configs': () => {
        calls += 1
        return calls === 1 ? problem('Pricing is unavailable.', 503) : json([item('com')])
      },
    })
    const { wrapper } = await mountScreen(TldConfigsView)

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tld-configs-error"]').text()).toBe('Pricing is unavailable.')
    })
    await wrapper.get('[data-testid="tld-configs-error-retry"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="tld-row-com"]').exists()).toBe(true)
    })
    await wrapper.get('[data-testid="tld-row-com"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    expect(wrapper.find('[data-testid="tld-delete-com"]').exists()).toBe(true)
  })
})
