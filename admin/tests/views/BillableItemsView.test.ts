/**
 * Mount tests for BillableItemsView: the table renders what the API sent, the search, the client, type and invoiced
 * filters and the page live in the address and are sent to the API (so rows past the first page are reachable), a bulk
 * delete asks first and reloads, and a refused load shows the API sentence.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import BillableItemsView from '@/views/BillableItemsView.vue'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/**
 * Builds a billable item as the list endpoint returns it.
 *
 * @param overrides - Fields to replace.
 * @returns The item.
 */
const item = (overrides: Record<string, unknown> = {}) => ({
  id: 5,
  clientId: 7,
  clientName: 'Ada Lovelace',
  description: 'Domain renewal',
  amount: 12,
  currency: 'USD',
  type: 'OneTime',
  recurringPeriod: null,
  isInvoiced: false,
  invoiceId: null,
  nextDueDate: null,
  createdAt: '2026-10-01T00:00:00Z',
  ...overrides,
})

/** The items endpoint answer: a domain charge and a support charge. */
const itemsPage = () =>
  json({ items: [item(), item({ id: 6, description: 'Support hours' })], pageNumber: 1, pageSize: 20, totalCount: 2, totalPages: 1 })

/** The clients endpoint answer: one client, for the picker and the chip of the client filter. */
const oneClient = () =>
  json({ items: [{ id: 7, firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.test', status: 'Active' }], totalCount: 1 })

/** The clients endpoint answer: nobody (the filter is not under test). */
const noClients = () => json({ items: [], totalCount: 0 })

/**
 * Mounts the list at an address and waits for its first load.
 *
 * @param url - The address to open, with its query.
 * @returns The wrapper and the router.
 */
const mountListAt = async (url: string) => {
  const screen = await mountRouted(BillableItemsView, '/billing/billable-items', url)
  await flushPromises()
  return screen
}

/**
 * Mounts the list at its plain address and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountList = async () => (await mountListAt('/billing/billable-items')).wrapper

/**
 * Finds one of the filter selects by its test id.
 *
 * @param wrapper - The mounted list.
 * @param testid - The test id of the select.
 * @returns The select.
 */
const selectOf = (wrapper: VueWrapper, testid: string) => {
  const select = wrapper.findAllComponents(UiSelect).find((candidate) => candidate.attributes('data-testid') === testid)
  if (select === undefined) {
    throw new Error(`No select has the test id ${testid}`)
  }
  return select
}

/**
 * Reads the requests the list made to the items endpoint.
 *
 * @param mock - The fetch mock.
 * @returns The request urls, oldest first.
 */
const itemUrls = (mock: ReturnType<typeof stubRoutes>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/billing/billable-items'))

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('BillableItemsView', () => {
  it('renders a row per item with the amount and invoiced state', async () => {
    stubRoutes({ 'GET /api/billing/billable-items': itemsPage, 'GET /api/clients': noClients })
    const wrapper = await mountList()

    const rows = wrapper.findAll('[data-testid="billing-billable-items-table"] tbody tr')
    expect(rows).toHaveLength(2)
    expect(rows[0]?.text()).toContain('12.00 USD')
    expect(rows[0]?.text()).toContain('No')
  })

  it('counts every match the API reports, not only the rows on the page', async () => {
    stubRoutes({
      'GET /api/billing/billable-items': () => json({ items: [item()], pageNumber: 1, pageSize: 20, totalCount: 250, totalPages: 13 }),
      'GET /api/clients': noClients,
    })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="billing-billable-items-count"]').text()).toContain('250 Records Found')
  })

  it('asks the API for the first page with no filters', async () => {
    const mock = stubRoutes({ 'GET /api/billing/billable-items': itemsPage, 'GET /api/clients': noClients })
    await mountList()

    expect(itemUrls(mock)).toEqual(['/api/billing/billable-items?page=1&pageSize=20'])
  })

  it('reads the search, the filters and the page from the address, sends them and shows the chips', async () => {
    const mock = stubRoutes({ 'GET /api/billing/billable-items': itemsPage, 'GET /api/clients': oneClient })
    const { wrapper } = await mountListAt('/billing/billable-items?search=support&clientId=7&type=Recurring&invoiced=false&page=2')

    expect(itemUrls(mock)[0]).toBe('/api/billing/billable-items?page=2&pageSize=20&search=support&clientId=7&type=Recurring&invoiced=false')
    expect(wrapper.get('[data-testid="filter-chip-clientId"]').text()).toContain('Ada Lovelace')
    expect(wrapper.get('[data-testid="filter-chip-type"]').text()).toContain('Recurring')
    expect(wrapper.get('[data-testid="filter-chip-invoiced"]').text()).toContain('No')
  })

  it('puts the chosen type and invoiced state in the address and sends them', async () => {
    const mock = stubRoutes({ 'GET /api/billing/billable-items': itemsPage, 'GET /api/clients': noClients })
    const { wrapper, router } = await mountListAt('/billing/billable-items')

    await wrapper.get('[data-testid="billing-billable-items-filter-toggle"]').trigger('click')
    await flushPromises()
    selectOf(wrapper, 'billing-billable-items-filter-type').vm.$emit('update:modelValue', 'Recurring')
    selectOf(wrapper, 'billing-billable-items-filter-invoiced').vm.$emit('update:modelValue', 'true')
    await flushPromises()
    await wrapper.get('[data-testid="filter-apply"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ type: 'Recurring', invoiced: 'true' })
    expect(itemUrls(mock).at(-1)).toBe('/api/billing/billable-items?page=1&pageSize=20&type=Recurring&invoiced=true')
  })

  it('puts the chosen client in the address and sends it', async () => {
    const mock = stubRoutes({ 'GET /api/billing/billable-items': itemsPage, 'GET /api/clients': oneClient })
    const { wrapper, router } = await mountListAt('/billing/billable-items')

    await wrapper.get('[data-testid="billing-billable-items-filter-toggle"]').trigger('click')
    await flushPromises()
    wrapper.getComponent(UiClientSelect).vm.$emit('update:modelValue', '7')
    await flushPromises()
    await wrapper.get('[data-testid="filter-apply"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ clientId: '7' })
    expect(itemUrls(mock).at(-1)).toBe('/api/billing/billable-items?page=1&pageSize=20&clientId=7')
  })

  it('offers any, yes and no for the invoiced state', async () => {
    stubRoutes({ 'GET /api/billing/billable-items': itemsPage, 'GET /api/clients': noClients })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="billing-billable-items-filter-toggle"]').trigger('click')
    await flushPromises()

    const options = (selectOf(wrapper, 'billing-billable-items-filter-invoiced').props() as unknown as { options: { value: string; label: string }[] }).options
    expect(options).toEqual([
      { value: '', label: 'Any' },
      { value: 'true', label: 'Yes' },
      { value: 'false', label: 'No' },
    ])
  })

  it('clears a filter from its chip and reloads', async () => {
    const mock = stubRoutes({ 'GET /api/billing/billable-items': itemsPage, 'GET /api/clients': noClients })
    const { wrapper } = await mountListAt('/billing/billable-items?type=Recurring')

    await wrapper.get('[data-testid="filter-chip-remove-type"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="filter-chip-type"]').exists()).toBe(false)
    expect(itemUrls(mock).at(-1)).toBe('/api/billing/billable-items?page=1&pageSize=20')
  })

  it('loads the next page from the pager', async () => {
    const mock = stubRoutes({
      'GET /api/billing/billable-items': () => json({ items: [item()], pageNumber: 1, pageSize: 20, totalCount: 45, totalPages: 3 }),
      'GET /api/clients': noClients,
    })
    const { wrapper, router } = await mountListAt('/billing/billable-items')

    await wrapper.get('[data-testid="billing-billable-items-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(itemUrls(mock).at(-1)).toBe('/api/billing/billable-items?page=2&pageSize=20')
  })

  it('says no item matches when a narrowed list is empty, and clears the filters from there', async () => {
    stubRoutes({
      'GET /api/billing/billable-items': () => json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 1 }),
      'GET /api/clients': noClients,
    })
    const { wrapper } = await mountListAt('/billing/billable-items?type=Recurring')

    expect(wrapper.get('[data-testid="billing-billable-items-empty"]').text()).toContain('No billable items match your filters.')
    await wrapper.get('[data-testid="billing-billable-items-clear"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-billable-items-empty"]').text()).toContain('No items found.')
  })

  it('drops the ticked items when the list changes', async () => {
    stubRoutes({ 'GET /api/billing/billable-items': itemsPage, 'GET /api/clients': noClients })
    const { wrapper } = await mountListAt('/billing/billable-items?type=Recurring')
    await wrapper.get('[data-testid="billing-billable-item-select-5"]').trigger('click')
    expect(wrapper.find('[data-testid="billing-billable-items-bulk"]').exists()).toBe(true)

    stubRoutes({
      'GET /api/billing/billable-items': () => json({ items: [item({ id: 6 })], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }),
      'GET /api/clients': noClients,
    })
    await wrapper.get('[data-testid="filter-chip-remove-type"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-billable-items-bulk"]').exists()).toBe(false)
  })

  it('deletes the ticked items only after the confirmation, then reloads the page', async () => {
    const mock = stubRoutes({
      'GET /api/billing/billable-items': itemsPage,
      'GET /api/clients': noClients,
      'DELETE /api/billing/billable-items/5': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="billing-billable-item-select-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-billable-items-delete"]').trigger('click')
    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(true)
    expect(itemUrls(mock).filter((url) => url.includes('?page='))).toHaveLength(2)
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ 'GET /api/billing/billable-items': () => problem('Items are unavailable.', 403), 'GET /api/clients': noClients })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="billing-billable-items-error"]').text()).toContain('Items are unavailable.')
  })

  it('prints a billable item type as a name, not the value the API sends', async () => {
    stubRoutes({
      'GET /api/billing/billable-items': () =>
        json({ items: [item(), item({ id: 6, type: 'Recurring' })], pageNumber: 1, pageSize: 20, totalCount: 2, totalPages: 1 }),
      'GET /api/clients': noClients,
    })
    const wrapper = await mountList()

    const rows = wrapper.findAll('[data-testid="billing-billable-items-table"] tbody tr')
    expect(rows[0]?.text()).toContain('One time')
    expect(rows[0]?.text()).not.toContain('OneTime')
    expect(rows[1]?.text()).toContain('Recurring')
  })

  it('reports the deleted items next to the list', async () => {
    stubRoutes({
      'GET /api/billing/billable-items': itemsPage,
      'GET /api/clients': noClients,
      'DELETE /api/billing/billable-items/5': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="billing-billable-item-select-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-billable-items-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-billable-items-feedback"]').text()).toBe('Billable items deleted.')
  })

  it('shows the API sentence when a delete is refused, without the load error', async () => {
    stubRoutes({
      'GET /api/billing/billable-items': itemsPage,
      'GET /api/clients': noClients,
      'DELETE /api/billing/billable-items/5': () => problem('The item is already invoiced.', 409),
    })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="billing-billable-item-select-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-billable-items-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-billable-items-feedback"]').text()).toBe('The item is already invoiced.')
    expect(wrapper.find('[data-testid="billing-billable-items-error"]').exists()).toBe(false)
  })

  it('keeps the items when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/billing/billable-items': itemsPage, 'GET /api/clients': noClients })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="billing-billable-item-select-5"]').trigger('click')
    await wrapper.get('[data-testid="billing-billable-items-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
  })

  it('reloads the items from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({
      'GET /api/billing/billable-items': () => (refused ? problem('Items are unavailable.', 403) : itemsPage()),
      'GET /api/clients': noClients,
    })
    const wrapper = await mountList()

    refused = false
    await wrapper.get('[data-testid="billing-billable-items-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-billable-items-error"]').exists()).toBe(false)
  })
})
