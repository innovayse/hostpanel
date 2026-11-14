/**
 * Mount tests of CancellationRequestsView (cancellation requests): it renders what the API sent, sends the search, the
 * type and status filters and the page kept in the address, shows the empty state where it has one, confirms and reports
 * a delete, and shows the API's sentence with a Retry when the load is refused.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import CancellationRequestsView from '@/views/CancellationRequestsView.vue'
import { mountRouted, mountRoutedSettled } from '../helpers/mount'
import { json, problem } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'

/**
 * Mounts the list at an address and waits for its first load.
 *
 * @param url - The address to open, with its query.
 * @returns The wrapper and the router.
 */
const mountListAt = async (url: string) => {
  const screen = await mountRouted(CancellationRequestsView, '/clients/cancellations', url)
  await flushPromises()
  return screen
}

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
 * Reads the requests the list made.
 *
 * @param mock - The fetch mock.
 * @returns The request urls, oldest first.
 */
const listUrls = (mock: ReturnType<typeof stubRoutes>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/admin/cancellation-requests?'))

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('CancellationRequestsView', () => {
  /** One open, immediate cancellation as the list endpoint returns it. */
  const request = {
    id: 5,
    serviceId: 2,
    serviceName: 'Shared Hosting',
    clientId: 9,
    clientName: 'Grace Hopper',
    type: 'Immediate',
    reason: 'Moving away',
    status: 'Open',
    createdAt: '2026-05-01T09:00:00Z',
  }

  it('renders the request with its type and status words', async () => {
    stubRoutes({ 'GET /api/admin/cancellation-requests': () => json({ items: [request], totalCount: 1 }) })
    const wrapper = await mountRoutedSettled(CancellationRequestsView, '/clients/cancellations')

    const row = wrapper.get('[data-testid="cancellations-row-5"]').text()
    expect(row).toContain('Shared Hosting')
    expect(row).toContain('Immediate')
    expect(row).toContain('Open')
    expect(wrapper.get('[data-testid="cancellations-client-5"]').attributes('href')).toBe('/clients/9')
  })

  it('asks before deleting, then deletes and reloads', async () => {
    const mock = stubRoutes({
      'DELETE /api/admin/cancellation-requests/5': () => new Response(null, { status: 204 }),
      'GET /api/admin/cancellation-requests': () => json({ items: [request], totalCount: 1 }),
    })
    const wrapper = await mountRoutedSettled(CancellationRequestsView, '/clients/cancellations')

    await wrapper.get('[data-testid="cancellations-delete-5"]').trigger('click')
    expect(wrapper.find('[data-testid="cancellations-delete-dialog"]').exists()).toBe(true)
    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)

    await wrapper.get('[data-testid="cancellations-delete-dialog"] [data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(true)
  })

  it('shows the empty state and the refusal sentence', async () => {
    stubRoutes({ 'GET /api/admin/cancellation-requests': () => json({ items: [], totalCount: 0 }) })
    const empty = await mountRoutedSettled(CancellationRequestsView, '/clients/cancellations')
    expect(empty.get('[data-testid="cancellations-empty"]').text()).toContain('No cancellation requests found')

    stubRoutes({ 'GET /api/admin/cancellation-requests': () => problem('Not allowed.', 403) })
    const refused = await mountRoutedSettled(CancellationRequestsView, '/clients/cancellations')
    expect(refused.get('[data-testid="cancellations-error"]').text()).toContain('Not allowed.')
  })

  it('reports a successful delete next to the list', async () => {
    stubRoutes({
      'DELETE /api/admin/cancellation-requests/5': () => new Response(null, { status: 204 }),
      'GET /api/admin/cancellation-requests': () => json({ items: [request], totalCount: 1 }),
    })
    const wrapper = await mountRoutedSettled(CancellationRequestsView, '/clients/cancellations')

    await wrapper.get('[data-testid="cancellations-delete-5"]').trigger('click')
    await wrapper.get('[data-testid="cancellations-delete-dialog"] [data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="cancellations-feedback"]').text()).toBe('Cancellation request deleted.')
  })

  it('shows the API sentence when the delete is refused and keeps the list', async () => {
    stubRoutes({
      'DELETE /api/admin/cancellation-requests/5': () => problem('The request is being processed.', 409),
      'GET /api/admin/cancellation-requests': () => json({ items: [request], totalCount: 1 }),
    })
    const wrapper = await mountRoutedSettled(CancellationRequestsView, '/clients/cancellations')

    await wrapper.get('[data-testid="cancellations-delete-5"]').trigger('click')
    await wrapper.get('[data-testid="cancellations-delete-dialog"] [data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="cancellations-feedback"]').text()).toBe('The request is being processed.')
    expect(wrapper.find('[data-testid="cancellations-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="cancellations-row-5"]').exists()).toBe(true)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/admin/cancellation-requests': () => json({ items: [request], totalCount: 1 }) })
    const wrapper = await mountRoutedSettled(CancellationRequestsView, '/clients/cancellations')

    await wrapper.get('[data-testid="cancellations-delete-5"]').trigger('click')
    await wrapper.get('[data-testid="cancellations-delete-dialog"] [data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
    expect(wrapper.find('[data-testid="cancellations-delete-dialog"]').exists()).toBe(false)
  })

  it('asks the API for the first page with no filters', async () => {
    const mock = stubRoutes({ 'GET /api/admin/cancellation-requests': () => json({ items: [request], totalCount: 1 }) })
    await mountListAt('/clients/cancellations')

    expect(listUrls(mock)).toEqual(['/api/admin/cancellation-requests?page=1&pageSize=20'])
  })

  it('reads the search, the filters and the page from the address, sends them and shows the chips', async () => {
    const mock = stubRoutes({ 'GET /api/admin/cancellation-requests': () => json({ items: [request], totalCount: 1 }) })
    const { wrapper } = await mountListAt('/clients/cancellations?search=moving&type=EndOfBillingPeriod&status=Closed&page=2')

    expect(listUrls(mock)[0]).toBe('/api/admin/cancellation-requests?page=2&pageSize=20&search=moving&type=EndOfBillingPeriod&status=Closed')
    expect(wrapper.get('[data-testid="filter-chip-type"]').text()).toContain('End of Billing Period')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Closed')
  })

  it('puts the chosen type and status in the address and sends them', async () => {
    const mock = stubRoutes({ 'GET /api/admin/cancellation-requests': () => json({ items: [request], totalCount: 1 }) })
    const { wrapper, router } = await mountListAt('/clients/cancellations')

    await wrapper.get('[data-testid="cancellations-filter"]').trigger('click')
    await flushPromises()
    selectOf(wrapper, 'cancellations-filter-type').vm.$emit('update:modelValue', 'Immediate')
    selectOf(wrapper, 'cancellations-filter-status').vm.$emit('update:modelValue', 'Open')
    await flushPromises()
    await wrapper.get('[data-testid="filter-apply"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ type: 'Immediate', status: 'Open' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/cancellation-requests?page=1&pageSize=20&type=Immediate&status=Open')
  })

  it('offers any first, then each cancellation type and status', async () => {
    stubRoutes({ 'GET /api/admin/cancellation-requests': () => json({ items: [request], totalCount: 1 }) })
    const { wrapper } = await mountListAt('/clients/cancellations')

    await wrapper.get('[data-testid="cancellations-filter"]').trigger('click')
    await flushPromises()

    const optionsOf = (testid: string) =>
      (selectOf(wrapper, testid).props() as unknown as { options: { value: string }[] }).options.map((option) => option.value)
    expect(optionsOf('cancellations-filter-type')).toEqual(['', 'Immediate', 'EndOfBillingPeriod'])
    expect(optionsOf('cancellations-filter-status')).toEqual(['', 'Open', 'Closed'])
  })

  it('clears a filter from its chip and reloads', async () => {
    const mock = stubRoutes({ 'GET /api/admin/cancellation-requests': () => json({ items: [request], totalCount: 1 }) })
    const { wrapper } = await mountListAt('/clients/cancellations?status=Closed')

    await wrapper.get('[data-testid="filter-chip-remove-status"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="filter-chip-status"]').exists()).toBe(false)
    expect(listUrls(mock).at(-1)).toBe('/api/admin/cancellation-requests?page=1&pageSize=20')
  })

  it('loads the next page from the pager', async () => {
    const mock = stubRoutes({ 'GET /api/admin/cancellation-requests': () => json({ items: [request], totalCount: 45 }) })
    const { wrapper, router } = await mountListAt('/clients/cancellations')

    await wrapper.get('[data-testid="cancellations-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/cancellation-requests?page=2&pageSize=20')
  })

  it('says no request matches when a narrowed list is empty, and clears the filters from there', async () => {
    stubRoutes({ 'GET /api/admin/cancellation-requests': () => json({ items: [], totalCount: 0 }) })
    const { wrapper } = await mountListAt('/clients/cancellations?type=Immediate')

    expect(wrapper.get('[data-testid="cancellations-empty"]').text()).toContain('No cancellation requests match your filters')
    await wrapper.get('[data-testid="cancellations-clear-filters"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="cancellations-empty"]').text()).toContain('No cancellation requests found')
  })

  it('reloads the list from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({
      'GET /api/admin/cancellation-requests': () => (refused ? problem('Not allowed.', 403) : json({ items: [request], totalCount: 1 })),
    })
    const wrapper = await mountRoutedSettled(CancellationRequestsView, '/clients/cancellations')

    refused = false
    await wrapper.get('[data-testid="cancellations-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="cancellations-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="cancellations-row-5"]').exists()).toBe(true)
  })
})
