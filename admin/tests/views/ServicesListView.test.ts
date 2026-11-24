/**
 * Mount tests for ServicesListView: rows render with price, cycle, status label and a client
 * link, the search, the status, product and billing cycle filters and the page are kept in the address and sent to
 * the API, each applied filter shows as a removable chip, and a refused load or an empty result shows the right
 * state.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import ServicesListView from '@/views/ServicesListView.vue'
import schema from '@/views/ServicesListView.schema'
import { renderedCaptions, schemaCaptions } from '../helpers/schemaTable'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { BillingCycle } from '@/types/service'

/** One active service as the list endpoint returns it. */
const service = {
  id: 11,
  clientId: 3,
  clientName: 'Alan Turing',
  productName: 'VPS Small',
  domain: null,
  price: 4.99,
  priceCurrency: 'USD',
  billingCycle: BillingCycle.Monthly,
  status: 'Suspended',
  nextDueDate: '2026-05-01T00:00:00Z',
}

/** The products the product filter offers. */
const products = [
  { id: 1, name: 'Shared Starter' },
  { id: 2, name: 'VPS Small' },
]

/**
 * Stubs the list endpoint and the product lookup the filter dialog reads.
 *
 * @param items - The services the list answers with.
 * @param totalCount - The total the list answers with.
 * @returns The mock, for asserting the requests made.
 */
const stubList = (items: unknown[] = [service], totalCount = items.length): ReturnType<typeof vi.fn> =>
  stubRoutes({
    'GET /api/services': () => json({ items, totalCount }),
    'GET /api/products': () => json(products),
  })

/**
 * Lists the requests the mock has seen for the service list.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/services?'))

/**
 * Opens the filter dialog, chooses a value in one of its selects and applies it.
 *
 * @param wrapper - The mounted list.
 * @param testid - The `data-testid` of the select to use.
 * @param value - The option value to choose.
 */
const applyFilter = async (wrapper: VueWrapper, testid: string, value: string) => {
  await wrapper.get('[data-testid="services-filter"]').trigger('click')
  await flushPromises()
  const select = wrapper.findAllComponents(UiSelect).find((candidate) => candidate.attributes('data-testid') === testid)
  if (select === undefined) {
    throw new Error(`No select has the test id ${testid}`)
  }
  select.vm.$emit('update:modelValue', value)
  await flushPromises()
  await wrapper.get('[data-testid="filter-apply"]').trigger('click')
  await flushPromises()
}

/**
 * Mounts the list at an address and waits for its first load.
 *
 * @param url - The address to open, with its query.
 * @returns The wrapper and the router.
 */
const mountListAt = async (url: string) => {
  const screen = await mountRouted(ServicesListView, '/services', url)
  await flushPromises()
  return screen
}

/**
 * Mounts the list at its plain address and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountList = async () => (await mountListAt('/services')).wrapper

describe('ServicesListView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders a row with product, price, capitalised cycle, status and client link', async () => {
    stubRoutes({ 'GET /api/services': () => json({ items: [service], totalCount: 1 }) })
    const wrapper = await mountList()

    const row = wrapper.get('[data-testid="services-row-11"]')
    expect(row.text()).toContain('VPS Small')
    expect(row.text()).toContain('$4.99 USD')
    expect(row.text()).toContain('Monthly')
    expect(row.text()).toContain('Suspended')
    expect(wrapper.get('[data-testid="services-client-11"]').attributes('href')).toBe('/clients/3')
    const headers = wrapper.findAll('th')
    expect(headers.find((header) => header.text() === 'Product/Service')?.classes()).toEqual(expect.arrayContaining(['w-[28%]', 'max-w-0', 'truncate']))
    expect(headers.find((header) => header.text() === 'Price')?.classes()).toEqual(expect.arrayContaining(['w-px', 'whitespace-nowrap']))
  })

  it('draws the headers of the schema columns, in order', async () => {
    stubRoutes({ 'GET /api/services': () => json({ items: [service], totalCount: 1 }) })
    const wrapper = await mountList()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('loads the next page from the pager', async () => {
    const mock = stubRoutes({ 'GET /api/services': () => json({ items: [service], totalCount: 45 }) })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="services-pagination-next"]').trigger('click')
    await flushPromises()

    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls.some((url) => url.includes('page=2'))).toBe(true)
  })

  it('keeps the chosen page in the address', async () => {
    stubRoutes({ 'GET /api/services': () => json({ items: [service], totalCount: 45 }) })
    const { wrapper, router } = await mountListAt('/services')

    await wrapper.get('[data-testid="services-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
  })

  it('opens the page the address names', async () => {
    const mock = stubRoutes({ 'GET /api/services': () => json({ items: [service], totalCount: 45 }) })
    await mountListAt('/services?page=3')

    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls[0]).toBe('/api/services?page=3&pageSize=20')
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountListAt('/services')

    await wrapper.get('[data-testid="services-search"]').setValue('shop')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'shop' })
    expect(listUrls(mock).at(-1)).toBe('/api/services?page=1&pageSize=20&search=shop')
  })

  it('sends the status, the product and the billing cycle picked in the filter dialog and shows each as a chip', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountListAt('/services')

    await applyFilter(wrapper, 'services-filter-status', 'Suspended')
    expect(listUrls(mock).at(-1)).toBe('/api/services?page=1&pageSize=20&status=Suspended')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Suspended')

    await applyFilter(wrapper, 'services-filter-product', '2')
    expect(listUrls(mock).at(-1)).toBe('/api/services?page=1&pageSize=20&status=Suspended&productId=2')
    expect(wrapper.get('[data-testid="filter-chip-productId"]').text()).toContain('VPS Small')

    await applyFilter(wrapper, 'services-filter-billing-cycle', 'Annual')
    expect(router.currentRoute.value.query).toEqual({ status: 'Suspended', productId: '2', billingCycle: 'Annual' })
    expect(listUrls(mock).at(-1)).toBe('/api/services?page=1&pageSize=20&status=Suspended&productId=2&billingCycle=Annual')
    expect(wrapper.get('[data-testid="filter-chip-billingCycle"]').text()).toContain('Annual')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
  })

  it('restores the filters, the search and the page from the address and sends them together', async () => {
    const mock = stubList([service], 45)
    await mountListAt('/services?search=shop&status=Active&productId=1&billingCycle=Monthly&page=2')

    expect(listUrls(mock)[0]).toBe('/api/services?page=2&pageSize=20&search=shop&status=Active&productId=1&billingCycle=Monthly')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubList()
    await mountListAt('/services?status=Nonsense&productId=abc&billingCycle=Weekly')

    expect(listUrls(mock)[0]).toBe('/api/services?page=1&pageSize=20')
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountListAt('/services?status=Active&billingCycle=Monthly')

    await wrapper.get('[data-testid="filter-chip-remove-status"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ billingCycle: 'Monthly' })
    expect(listUrls(mock).at(-1)).toBe('/api/services?page=1&pageSize=20&billingCycle=Monthly')
  })

  it('names the empty list after the search or filters, and clears the filters from it', async () => {
    stubList([])
    const plain = await mountListAt('/services')
    expect(plain.wrapper.find('[data-testid="services-clear-empty"]').exists()).toBe(false)

    const narrowed = await mountListAt('/services?status=Active&search=zzz')
    expect(narrowed.wrapper.get('[data-testid="services-empty"]').text()).toContain('No services match your filters')

    await narrowed.wrapper.get('[data-testid="services-clear-empty"]').trigger('click')
    await flushPromises()

    expect(narrowed.router.currentRoute.value.query).toEqual({ search: 'zzz' })
  })

  it('shows the empty state when there are no services', async () => {
    stubList([])
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="services-empty"]').text()).toContain('No services found')
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ 'GET /api/services': () => problem('Not allowed.', 403) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="services-error"]').text()).toContain('Not allowed.')
  })

  it('loads again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/services': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json({ items: [service], totalCount: 1 })
      },
    })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="services-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="services-row-11"]').text()).toContain('VPS Small')
  })
})
