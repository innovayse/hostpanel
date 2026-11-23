/**
 * Tests for VatRatesView: the table, the search and the page kept in the address and sent to the API, the add and edit
 * dialog with its refusal, and the delete with its confirmation, all through the store at network level.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import VatRatesView from '@/views/VatRatesView.vue'
import schema from '@/views/VatRatesView.schema'
import UiCountrySelect from '@/components/ui/UiCountrySelect.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import { useToastStore } from '@/stores/toast'
import { VatRateKind } from '@/types/vatrate'
import type { VatRateDto } from '@/types/vatrate'
import { json, problem } from '../helpers/fetch'
import { applyListFilter } from '../helpers/listFilter'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { renderedCaptions, schemaCaptions } from '../helpers/schemaTable'
import { toastMessages } from '../helpers/toasts'

/** The address the screen is registered under. */
const PATH = '/settings/vat-rates'

/** The standard rate of Germany. */
const germany: VatRateDto = { id: 1, country: 'DE', kind: VatRateKind.Standard, ratePercent: 19, validFrom: '2024-01-01' }

/** A reduced rate of Germany. */
const reducedGermany: VatRateDto = { id: 2, country: 'DE', kind: VatRateKind.Reduced, ratePercent: 7, validFrom: '2024-01-01' }

/**
 * Wraps rows in the page shape the list endpoint answers with.
 *
 * @param items - The rows of the page.
 * @param totalCount - Matches across all pages; the page length by default.
 * @returns The JSON body.
 */
const page = (items: VatRateDto[], totalCount = items.length) => ({
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
  const screen = await mountRouted(VatRatesView, PATH, url)
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
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/admin/vat-rates?'))

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

describe('VatRatesView', () => {
  it('draws the table head from the schema columns', async () => {
    stubRoutes({ 'GET /api/admin/vat-rates?': () => json(page([germany])) })
    const { wrapper } = await mountListAt()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('lists the rates with country, kind, rate and the day they apply from', async () => {
    stubRoutes({ 'GET /api/admin/vat-rates?': () => json(page([germany, reducedGermany])) })
    const { wrapper } = await mountListAt()

    expect(wrapper.get('h1').text()).toBe('VAT Rates')
    const first = wrapper.get('[data-testid="vat-rate-row-1"]').text()
    expect(first).toContain('Germany')
    expect(first).toContain('Standard')
    expect(first).toContain('19%')
    expect(first).toContain('01/01/2024')
    const second = wrapper.get('[data-testid="vat-rate-row-2"]').text()
    expect(second).toContain('Reduced')
    expect(second).toContain('7%')
    expect(wrapper.findAll('th').find((header) => header.text() === 'Country')?.classes()).toEqual(expect.arrayContaining(['w-[28%]', 'max-w-0', 'truncate']))
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/admin/vat-rates?': () => json(page([germany])) })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="vat-rates-search"]').setValue('de')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'de' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/vat-rates?page=1&pageSize=20&search=de')
  })

  it('restores the search and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ 'GET /api/admin/vat-rates?': () => json(page([germany], 45)) })
    const { wrapper } = await mountListAt(`${PATH}?search=de&page=2`)

    expect(listUrls(mock)[0]).toBe('/api/admin/vat-rates?page=2&pageSize=20&search=de')
    expect((wrapper.get('[data-testid="vat-rates-search"]').element as HTMLInputElement).value).toBe('de')
  })

  it('puts the chosen filters in the address and sends them with the list request', async () => {
    const mock = stubRoutes({ 'GET /api/admin/vat-rates?': () => json(page([germany])) })
    const { wrapper, router } = await mountListAt()

    await applyListFilter(wrapper, { 'vat-rates-filter-kind': 'Reduced', 'vat-rates-filter-status': 'Superseded' })

    expect(router.currentRoute.value.query).toEqual({ kind: 'Reduced', status: 'Superseded' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/vat-rates?page=1&pageSize=20&kind=Reduced&status=Superseded')
    expect(wrapper.text()).toContain('Superseded')
  })

  it('restores the filters from the address', async () => {
    const mock = stubRoutes({ 'GET /api/admin/vat-rates?': () => json(page([germany])) })

    await mountListAt(`${PATH}?kind=Standard&status=Active`)

    expect(listUrls(mock)[0]).toBe('/api/admin/vat-rates?page=1&pageSize=20&kind=Standard&status=Active')
  })

  it('keeps the chosen page in the address', async () => {
    stubRoutes({ 'GET /api/admin/vat-rates?': () => json(page([germany], 45)) })
    const { wrapper, router } = await mountListAt()

    expect(wrapper.find('[data-testid="ui-card-footer"] [data-testid="vat-rates-pagination-next"]').exists()).toBe(true)

    await wrapper.get('[data-testid="vat-rates-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
  })

  it('shows the empty state, and the no-match sentence when the list is narrowed', async () => {
    stubRoutes({ 'GET /api/admin/vat-rates?': () => json(page([])) })
    const { wrapper } = await mountListAt()
    expect(wrapper.get('[data-testid="vat-rates-empty"]').text()).toContain('No VAT rates yet')

    const narrowed = (await mountListAt(`${PATH}?search=zzz`)).wrapper
    expect(narrowed.get('[data-testid="vat-rates-empty"]').text()).toContain('No VAT rates match your search')
  })

  it('shows a load refusal and loads again from its retry', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/admin/vat-rates?': () => {
        calls += 1
        return calls === 1 ? problem('VAT rates are unavailable.', 503) : json(page([germany]))
      },
    })
    const { wrapper } = await mountListAt()
    expect(wrapper.get('[data-testid="vat-rates-error"]').text()).toBe('VAT rates are unavailable.')

    await wrapper.get('[data-testid="vat-rates-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="vat-rate-row-1"]').exists()).toBe(true)
  })

  it('adds a rate through POST, closes the dialog and reloads the page', async () => {
    const mock = stubRoutes({
      'GET /api/admin/vat-rates?': () => json(page([germany])),
      'POST /api/admin/vat-rates': () => json(9, 201),
    })
    const { wrapper } = await mountListAt(`${PATH}?search=de`)
    const before = listUrls(mock).length

    await wrapper.get('[data-testid="vat-rates-add"]').trigger('click')
    wrapper.getComponent(UiCountrySelect).vm.$emit('update:modelValue', 'FR')
    wrapper.getComponent(UiDatePicker).vm.$emit('update:modelValue', '2025-07-01')
    await wrapper.get('[data-testid="vat-rate-form"]').trigger('submit')
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="vat-rate-form"]').exists()).toBe(false)
    })

    expect(requestsOf(mock)).toContain('POST /api/admin/vat-rates')
    await vi.waitFor(() => {
      expect(listUrls(mock).length).toBe(before + 1)
    })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/vat-rates?page=1&pageSize=20&search=de')
  })

  it('keeps the dialog open and shows the refusal of a save', async () => {
    stubRoutes({
      'GET /api/admin/vat-rates?': () => json(page([germany])),
      'POST /api/admin/vat-rates': () => problem('A VAT rate for this day exists.', 409),
    })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="vat-rates-add"]').trigger('click')
    wrapper.getComponent(UiCountrySelect).vm.$emit('update:modelValue', 'DE')
    wrapper.getComponent(UiDatePicker).vm.$emit('update:modelValue', '2024-01-01')
    await wrapper.get('[data-testid="vat-rate-form"]').trigger('submit')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="vat-rate-form-error"]').text()).toBe('A VAT rate for this day exists.')
    })
  })

  it('opens the edit dialog with the stored values and saves through PUT', async () => {
    const mock = stubRoutes({
      'GET /api/admin/vat-rates?': () => json(page([germany, reducedGermany])),
      'PUT /api/admin/vat-rates/2': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="vat-rate-actions-2"]').trigger('click')
    await wrapper.get('[data-testid="vat-rate-edit-2"]').trigger('click')
    expect(wrapper.getComponent(UiCountrySelect).props('modelValue')).toBe('DE')
    expect(wrapper.getComponent(UiDatePicker).props('modelValue')).toBe('2024-01-01')

    await wrapper.get('[data-testid="vat-rate-form"]').trigger('submit')
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="vat-rate-form"]').exists()).toBe(false)
    })

    expect(requestsOf(mock)).toContain('PUT /api/admin/vat-rates/2')
  })

  it('asks before deleting, then deletes, reports it and reloads the page', async () => {
    const mock = stubRoutes({
      'GET /api/admin/vat-rates?': () => json(page([germany])),
      'DELETE /api/admin/vat-rates/1': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountListAt()
    const before = listUrls(mock).length

    await wrapper.get('[data-testid="vat-rate-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="vat-rate-delete-1"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Delete the Standard rate of Germany that applies from 01/01/2024?')
    expect(requestsOf(mock)).not.toContain('DELETE /api/admin/vat-rates/1')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['The VAT rate was deleted.'])
    })
    expect(requestsOf(mock)).toContain('DELETE /api/admin/vat-rates/1')
    expect(listUrls(mock).length).toBe(before + 1)
  })

  it('steps back a page when the delete empties the last one', async () => {
    let deleted = false
    const mock = stubRoutes({
      'GET /api/admin/vat-rates?': () => json(page(deleted ? [] : [germany], deleted ? 20 : 21)),
      'DELETE /api/admin/vat-rates/1': () => {
        deleted = true
        return new Response(null, { status: 204 })
      },
    })
    const { wrapper, router } = await mountListAt(`${PATH}?page=2`)

    await wrapper.get('[data-testid="vat-rate-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="vat-rate-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(router.currentRoute.value.query.page).toBeUndefined()
    })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/vat-rates?page=1&pageSize=20')
  })

  it('shows the refusal of a delete in the result banner', async () => {
    stubRoutes({
      'GET /api/admin/vat-rates?': () => json(page([germany])),
      'DELETE /api/admin/vat-rates/1': () => problem('The VAT rate does not exist.', 404),
    })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="vat-rate-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="vat-rate-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(toastMessages()).toContain('The VAT rate does not exist.')
    })
  })
})
