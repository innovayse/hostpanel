/**
 * Tests for TaxRulesView: the table, the search, the enabled filter and the page kept in the address and sent to the
 * API, each applied filter as a removable chip, the create and edit dialog with its refusal, and the delete with its
 * confirmation, all through the store at network level.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import TaxRulesView from '@/views/TaxRulesView.vue'
import UiCountrySelect from '@/components/ui/UiCountrySelect.vue'
import { EnabledFilter } from '@/types/enabledfilter'
import type { TaxRuleDto } from '@/types/taxrule'
import { json, problem } from '../helpers/fetch'
import { applyListFilter } from '../helpers/listFilter'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** The address the screen is registered under. */
const PATH = '/settings/tax-rules'

/** A country-wide rule. */
const germany: TaxRuleDto = { id: 1, name: 'Germany VAT', country: 'DE', region: null, ratePercent: 19, isEnabled: true }

/** A disabled regional rule. */
const bavaria: TaxRuleDto = { id: 2, name: 'Bavaria VAT', country: 'DE', region: 'Bavaria', ratePercent: 7.5, isEnabled: false }

/**
 * Wraps rows in the page shape the list endpoint answers with.
 *
 * @param items - The rows of the page.
 * @param totalCount - Matches across all pages; the page length by default.
 * @returns The JSON body.
 */
const page = (items: TaxRuleDto[], totalCount = items.length) => ({
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
  const screen = await mountRouted(TaxRulesView, PATH, url)
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
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/admin/tax-rules?'))

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

describe('TaxRulesView', () => {
  it('lists the rules with country, region, rate and status', async () => {
    stubRoutes({ 'GET /api/admin/tax-rules?': () => json(page([germany, bavaria])) })
    const { wrapper } = await mountListAt()

    expect(wrapper.get('h1').text()).toBe('Tax Rules')
    const first = wrapper.get('[data-testid="tax-rule-row-1"]').text()
    expect(first).toContain('Germany VAT')
    expect(first).toContain('Germany')
    expect(first).toContain('Whole country')
    expect(first).toContain('19%')
    expect(first).toContain('Enabled')
    const second = wrapper.get('[data-testid="tax-rule-row-2"]').text()
    expect(second).toContain('Bavaria')
    expect(second).toContain('7.5%')
    expect(second).toContain('Disabled')
    expect(wrapper.text()).toContain('Tax rules: 2')
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/admin/tax-rules?': () => json(page([germany])) })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="tax-rules-search"]').setValue('vat')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'vat' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/tax-rules?page=1&pageSize=20&search=vat')
  })

  it('restores the search, the enabled filter and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ 'GET /api/admin/tax-rules?': () => json(page([germany], 45)) })
    const { wrapper } = await mountListAt(`${PATH}?search=vat&enabled=disabled&page=2`)

    expect(listUrls(mock)[0]).toBe('/api/admin/tax-rules?page=2&pageSize=20&search=vat&enabled=false')
    expect((wrapper.get('[data-testid="tax-rules-search"]').element as HTMLInputElement).value).toBe('vat')
    expect(wrapper.get('[data-testid="filter-chip-enabled"]').text()).toContain('Disabled')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('1')
  })

  it('keeps the chosen page in the address', async () => {
    stubRoutes({ 'GET /api/admin/tax-rules?': () => json(page([germany], 45)) })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="tax-rules-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
  })

  it('sends the enabled state picked in the filter dialog on Apply and shows it as a chip', async () => {
    const mock = stubRoutes({ 'GET /api/admin/tax-rules?': () => json(page([germany])) })
    const { wrapper, router } = await mountListAt()

    await applyListFilter(wrapper, { 'tax-rules-filter-enabled': EnabledFilter.Enabled })

    expect(router.currentRoute.value.query).toEqual({ enabled: 'enabled' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/tax-rules?page=1&pageSize=20&enabled=true')
    expect(wrapper.get('[data-testid="filter-chip-enabled"]').text()).toContain('Enabled')
  })

  it('does not send an enabled state the address holds that is not a known value', async () => {
    const mock = stubRoutes({ 'GET /api/admin/tax-rules?': () => json(page([germany])) })
    await mountListAt(`${PATH}?enabled=maybe`)

    expect(listUrls(mock)[0]).toBe('/api/admin/tax-rules?page=1&pageSize=20')
  })

  it('removes the enabled filter from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/admin/tax-rules?': () => json(page([germany])) })
    const { wrapper, router } = await mountListAt(`${PATH}?enabled=enabled&search=vat`)

    await wrapper.get('[data-testid="filter-chip-remove-enabled"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'vat' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/tax-rules?page=1&pageSize=20&search=vat')
  })

  it('shows the empty state, and the filter hint when the list is narrowed', async () => {
    stubRoutes({ 'GET /api/admin/tax-rules?': () => json(page([])) })
    const { wrapper } = await mountListAt()
    expect(wrapper.get('[data-testid="tax-rules-empty"]').text()).toContain('No tax rules yet')
    expect(wrapper.find('[data-testid="tax-rules-clear-empty"]').exists()).toBe(false)

    const narrowed = (await mountListAt(`${PATH}?search=zzz&enabled=enabled`)).wrapper
    expect(narrowed.get('[data-testid="tax-rules-empty"]').text()).toContain('No tax rules match your filters')
    expect(narrowed.find('[data-testid="tax-rules-clear-empty"]').exists()).toBe(true)
  })

  it('clears the filters from the empty state and keeps the search', async () => {
    stubRoutes({ 'GET /api/admin/tax-rules?': () => json(page([])) })
    const { wrapper, router } = await mountListAt(`${PATH}?enabled=enabled&search=zzz`)

    await wrapper.get('[data-testid="tax-rules-clear-empty"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'zzz' })
  })

  it('shows a load refusal and loads again from its retry', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/admin/tax-rules?': () => {
        calls += 1
        return calls === 1 ? problem('Tax rules are unavailable.', 503) : json(page([germany]))
      },
    })
    const { wrapper } = await mountListAt()
    expect(wrapper.get('[data-testid="tax-rules-error"]').text()).toBe('Tax rules are unavailable.')

    await wrapper.get('[data-testid="tax-rules-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="tax-rule-row-1"]').exists()).toBe(true)
  })

  it('creates a rule through POST, closes the dialog and reloads the page', async () => {
    const mock = stubRoutes({
      'GET /api/admin/tax-rules?': () => json(page([germany])),
      'POST /api/admin/tax-rules': () => json(9, 201),
    })
    const { wrapper } = await mountListAt(`${PATH}?search=vat`)
    const before = listUrls(mock).length

    await wrapper.get('[data-testid="tax-rules-add"]').trigger('click')
    await wrapper.get('[data-testid="tax-rule-form-name"]').setValue('France VAT')
    wrapper.getComponent(UiCountrySelect).vm.$emit('update:modelValue', 'FR')
    await wrapper.get('[data-testid="tax-rule-form"]').trigger('submit')
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="tax-rule-form"]').exists()).toBe(false)
    })

    expect(requestsOf(mock)).toContain('POST /api/admin/tax-rules')
    await vi.waitFor(() => {
      expect(listUrls(mock).length).toBe(before + 1)
    })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/tax-rules?page=1&pageSize=20&search=vat')
  })

  it('keeps the dialog open and shows the refusal of a save', async () => {
    stubRoutes({
      'GET /api/admin/tax-rules?': () => json(page([germany])),
      'POST /api/admin/tax-rules': () => problem('A rule for this country exists.', 409),
    })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="tax-rules-add"]').trigger('click')
    await wrapper.get('[data-testid="tax-rule-form"]').trigger('submit')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tax-rule-form-error"]').text()).toBe('A rule for this country exists.')
    })
  })

  it('opens the edit dialog with the stored values and saves through PUT', async () => {
    const mock = stubRoutes({
      'GET /api/admin/tax-rules?': () => json(page([germany, bavaria])),
      'PUT /api/admin/tax-rules/2': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="tax-rule-actions-2"]').trigger('click')
    await wrapper.get('[data-testid="tax-rule-edit-2"]').trigger('click')
    expect((wrapper.get('[data-testid="tax-rule-form-region"]').element as HTMLInputElement).value).toBe('Bavaria')

    await wrapper.get('[data-testid="tax-rule-form"]').trigger('submit')
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="tax-rule-form"]').exists()).toBe(false)
    })

    expect(requestsOf(mock)).toContain('PUT /api/admin/tax-rules/2')
  })

  it('asks before deleting, then deletes, reports it and reloads the page', async () => {
    const mock = stubRoutes({
      'GET /api/admin/tax-rules?': () => json(page([germany])),
      'DELETE /api/admin/tax-rules/1': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountListAt()
    const before = listUrls(mock).length

    await wrapper.get('[data-testid="tax-rule-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="tax-rule-delete-1"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Delete the tax rule Germany VAT?')
    expect(requestsOf(mock)).not.toContain('DELETE /api/admin/tax-rules/1')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tax-rules-result"]').text()).toBe('The tax rule was deleted.')
    })
    expect(requestsOf(mock)).toContain('DELETE /api/admin/tax-rules/1')
    expect(listUrls(mock).length).toBe(before + 1)
  })

  it('steps back a page when the delete empties the last one', async () => {
    let deleted = false
    const mock = stubRoutes({
      'GET /api/admin/tax-rules?': () => json(page(deleted ? [] : [germany], deleted ? 20 : 21)),
      'DELETE /api/admin/tax-rules/1': () => {
        deleted = true
        return new Response(null, { status: 204 })
      },
    })
    const { wrapper, router } = await mountListAt(`${PATH}?page=2`)

    await wrapper.get('[data-testid="tax-rule-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="tax-rule-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(router.currentRoute.value.query.page).toBeUndefined()
    })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/tax-rules?page=1&pageSize=20')
  })

  it('shows the refusal of a delete in the result banner', async () => {
    stubRoutes({
      'GET /api/admin/tax-rules?': () => json(page([germany])),
      'DELETE /api/admin/tax-rules/1': () => problem('The tax rule does not exist.', 404),
    })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="tax-rule-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="tax-rule-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tax-rules-result"]').text()).toBe('The tax rule does not exist.')
    })
  })
})
