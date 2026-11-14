/**
 * Smoke tests for TldConfigsView: the table with prices and margin, the search, the registrar and enabled filters and
 * the page kept in the address and sent to the API, each applied filter as a removable chip, import and sync with their
 * result banner, the delete confirmation, and the empty and refused states, at network level.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import TldConfigsView from '@/views/TldConfigsView.vue'
import { EnabledFilter } from '@/types/enabledfilter'
import { RegistrarModule } from '@/types/registrarmodule'
import type { TldConfigListItem } from '@/types/tldconfig'
import { json, problem } from '../helpers/fetch'
import { applyListFilter } from '../helpers/listFilter'
import { mountRouted } from '../helpers/mount'
import { pickOption } from '../helpers/select'
import { stubRoutes } from '../helpers/routes'

/** The address the screen is registered under. */
const PATH = '/settings/tld-pricing'

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

/**
 * Wraps rows in the page shape the list endpoint answers with.
 *
 * @param items - The rows of the page.
 * @param totalCount - Matches across all pages; the page length by default.
 * @returns The JSON body.
 */
const page = (items: TldConfigListItem[], totalCount = items.length) => ({
  items,
  pageNumber: 1,
  pageSize: 20,
  totalCount,
  totalPages: Math.max(1, Math.ceil(totalCount / 20)),
})

/**
 * Mounts the list at an address and waits for its first load.
 *
 * @param url - The address to open, with its query.
 * @returns The wrapper and the router.
 */
const mountListAt = async (url: string = PATH) => {
  const screen = await mountRouted(TldConfigsView, PATH, url)
  await flushPromises()
  return screen
}

/**
 * Lists the list requests the mock has seen.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/admin/tld-configs?'))

/**
 * Lists the method and URL of every request the mock has seen.
 *
 * @param mock - The stubbed fetch.
 * @returns One `METHOD url` line per request.
 */
const requestsOf = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url, init]) => `${(init as RequestInit | undefined)?.method ?? 'GET'} ${url as string}`)

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('TldConfigsView', () => {
  it('shows a row per TLD with prices in their symbols, margin, categories and sync state', async () => {
    stubRoutes({ 'GET /api/admin/tld-configs': () => json(page([item('com'), item('net', { id: 2, marginPercent: -5, isEnabled: false, categories: [] })])) })
    const { wrapper } = await mountListAt()
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
    expect(wrapper.text()).toContain('TLDs: 2')
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/admin/tld-configs': () => json(page([item('com')])) })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="tld-configs-search"]').setValue('co')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'co' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/tld-configs?page=1&pageSize=20&search=co')
  })

  it('restores the search, the filters and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ 'GET /api/admin/tld-configs': () => json(page([item('com')], 45)) })
    const { wrapper } = await mountListAt(`${PATH}?search=co&registrarModule=Namecheap&enabled=disabled&page=2`)

    expect(listUrls(mock)[0]).toBe('/api/admin/tld-configs?page=2&pageSize=20&search=co&registrarModule=Namecheap&enabled=false')
    expect((wrapper.get('[data-testid="tld-configs-search"]').element as HTMLInputElement).value).toBe('co')
    expect(wrapper.get('[data-testid="filter-chip-registrarModule"]').text()).toContain('Namecheap')
    expect(wrapper.get('[data-testid="filter-chip-enabled"]').text()).toContain('Disabled')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('2')
  })

  it('keeps the chosen page in the address', async () => {
    stubRoutes({ 'GET /api/admin/tld-configs': () => json(page([item('com')], 45)) })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="tld-configs-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
  })

  it('sends the registrar picked in the filter dialog on Apply and shows it as a chip', async () => {
    const mock = stubRoutes({ 'GET /api/admin/tld-configs': () => json(page([item('com')])) })
    const { wrapper, router } = await mountListAt()

    await applyListFilter(wrapper, { 'tld-configs-filter-registrar': RegistrarModule.Namecheap })

    expect(router.currentRoute.value.query).toEqual({ registrarModule: 'Namecheap' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/tld-configs?page=1&pageSize=20&registrarModule=Namecheap')
    expect(wrapper.get('[data-testid="filter-chip-registrarModule"]').text()).toContain('Namecheap')
  })

  it('sends the enabled state picked in the filter dialog on Apply', async () => {
    const mock = stubRoutes({ 'GET /api/admin/tld-configs': () => json(page([item('com')])) })
    const { wrapper, router } = await mountListAt()

    await applyListFilter(wrapper, { 'tld-configs-filter-enabled': EnabledFilter.Enabled })

    expect(router.currentRoute.value.query).toEqual({ enabled: 'enabled' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/tld-configs?page=1&pageSize=20&enabled=true')
    expect(wrapper.get('[data-testid="filter-chip-enabled"]').text()).toContain('Enabled')
  })

  it('does not send a registrar or enabled state the address holds that is not a known value', async () => {
    const mock = stubRoutes({ 'GET /api/admin/tld-configs': () => json(page([item('com')])) })
    await mountListAt(`${PATH}?registrarModule=Nonsense&enabled=maybe`)

    expect(listUrls(mock)[0]).toBe('/api/admin/tld-configs?page=1&pageSize=20')
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/admin/tld-configs': () => json(page([item('com')])) })
    const { wrapper, router } = await mountListAt(`${PATH}?registrarModule=NameAm&enabled=enabled`)

    await wrapper.get('[data-testid="filter-chip-remove-registrarModule"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ enabled: 'enabled' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/tld-configs?page=1&pageSize=20&enabled=true')
  })

  it('imports from a provider chosen in the menu, reports the counts and reloads the page', async () => {
    const mock = stubRoutes({
      'GET /api/admin/tld-configs?': () => json(page([item('com')])),
      'POST /api/admin/tld-configs/import/Namecheap': () => json({ imported: 2, updated: 1 }),
    })
    const { wrapper } = await mountListAt(`${PATH}?search=co&page=1`)
    const before = listUrls(mock).length

    await pickOption(wrapper, 'tld-configs-import', 'Namecheap')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tld-configs-result"]').text()).toBe('Import complete: 2 imported, 1 updated.')
    })
    expect(requestsOf(mock)).toContain('POST /api/admin/tld-configs/import/Namecheap')
    expect(listUrls(mock).length).toBe(before + 1)
    expect(listUrls(mock).at(-1)).toBe('/api/admin/tld-configs?page=1&pageSize=20&search=co')
  })

  it('syncs prices and shows a refusal in the banner', async () => {
    stubRoutes({
      'GET /api/admin/tld-configs?': () => json(page([item('com')])),
      'POST /api/admin/tld-configs/sync': () => problem('The registrar did not answer.', 502),
    })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="tld-configs-sync"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tld-configs-result"]').text()).toBe('The registrar did not answer.')
    })
  })

  it('asks before deleting a TLD, deletes it and reloads the page', async () => {
    const id = item('com').id
    const mock = stubRoutes({
      'GET /api/admin/tld-configs?': () => json(page([item('com')])),
      [`DELETE /api/admin/tld-configs/${id}`]: () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountListAt()
    const before = listUrls(mock).length

    await wrapper.get('[data-testid="tld-actions-com"]').trigger('click')
    await wrapper.get('[data-testid="tld-delete-com"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Delete ".com" TLD configuration?')
    expect(requestsOf(mock)).not.toContain(`DELETE /api/admin/tld-configs/${id}`)

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain(`DELETE /api/admin/tld-configs/${id}`)
    })
    await vi.waitFor(() => {
      expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    })
    expect(wrapper.get('[data-testid="tld-configs-result"]').text()).toBe('Record deleted.')
    expect(listUrls(mock).length).toBe(before + 1)
  })

  it('steps back a page when the delete empties the last one', async () => {
    const id = item('com').id
    let deleted = false
    const mock = stubRoutes({
      'GET /api/admin/tld-configs?': () => json(page(deleted ? [] : [item('com')], deleted ? 20 : 21)),
      [`DELETE /api/admin/tld-configs/${id}`]: () => {
        deleted = true
        return new Response(null, { status: 204 })
      },
    })
    const { wrapper, router } = await mountListAt(`${PATH}?page=2`)

    await wrapper.get('[data-testid="tld-actions-com"]').trigger('click')
    await wrapper.get('[data-testid="tld-delete-com"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(router.currentRoute.value.query.page).toBeUndefined()
    })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/tld-configs?page=1&pageSize=20')
  })

  it('shows the API sentence when a delete is refused, and sends nothing when dismissed', async () => {
    const id = item('com').id
    const mock = stubRoutes({
      'GET /api/admin/tld-configs?': () => json(page([item('com')])),
      [`DELETE /api/admin/tld-configs/${id}`]: () => problem('The TLD has domains.', 409),
    })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="tld-actions-com"]').trigger('click')
    await wrapper.get('[data-testid="tld-delete-com"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    expect(requestsOf(mock)).not.toContain(`DELETE /api/admin/tld-configs/${id}`)

    await wrapper.get('[data-testid="tld-actions-com"]').trigger('click')
    await wrapper.get('[data-testid="tld-delete-com"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tld-configs-result"]').text()).toBe('The TLD has domains.')
    })
  })

  it('navigates to the create and edit pages', async () => {
    stubRoutes({ 'GET /api/admin/tld-configs?': () => json(page([item('com')])) })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="tld-configs-add"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/settings/tld-pricing/create')
    })

    await router.push(PATH)
    await flushPromises()
    await wrapper.get('[data-testid="tld-actions-com"]').trigger('click')
    await wrapper.get('[data-testid="tld-edit-com"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe(`/settings/tld-pricing/${item('com').id}/edit`)
    })
  })

  it('shows the empty state, and the filter hint when the list is narrowed', async () => {
    stubRoutes({ 'GET /api/admin/tld-configs?': () => json(page([])) })
    const { wrapper } = await mountListAt()
    expect(wrapper.get('[data-testid="tld-configs-empty"]').text()).toContain('No TLD configurations found')
    expect(wrapper.find('[data-testid="tld-configs-clear-empty"]').exists()).toBe(false)

    const narrowed = (await mountListAt(`${PATH}?search=zzz&enabled=enabled`)).wrapper
    expect(narrowed.get('[data-testid="tld-configs-empty"]').text()).toContain('No TLDs match your filters')
    expect(narrowed.find('[data-testid="tld-configs-clear-empty"]').exists()).toBe(true)
  })

  it('clears the filters from the empty state and keeps the search', async () => {
    stubRoutes({ 'GET /api/admin/tld-configs?': () => json(page([])) })
    const { wrapper, router } = await mountListAt(`${PATH}?enabled=enabled&search=zzz`)

    await wrapper.get('[data-testid="tld-configs-clear-empty"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'zzz' })
  })

  it('shows a load refusal and loads again from its retry', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/admin/tld-configs?': () => {
        calls += 1
        return calls === 1 ? problem('Pricing is unavailable.', 503) : json(page([item('com')]))
      },
    })
    const { wrapper } = await mountListAt()

    expect(wrapper.get('[data-testid="tld-configs-error"]').text()).toBe('Pricing is unavailable.')
    await wrapper.get('[data-testid="tld-configs-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="tld-row-com"]').exists()).toBe(true)
    await wrapper.get('[data-testid="tld-actions-com"]').trigger('click')
    expect(wrapper.find('[data-testid="tld-delete-com"]').exists()).toBe(true)
  })
})
