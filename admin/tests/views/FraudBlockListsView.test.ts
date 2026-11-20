/**
 * Tests for FraudBlockListsView: the list on show comes from the API, the tab, the search and the page live in the
 * address, the tabs switch lists (starting from the first page with an empty search), an add sends the typed value and
 * re-reads the list, a refused add shows under its field or in the banner, and a delete asks for a confirmation first.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import FraudBlockListsView from '@/views/FraudBlockListsView.vue'
import { BlockListType } from '@/types/fraud'
import { BLOCK_LIST_TABLE, BLOCK_LIST_TABS_PART } from '@/views/FraudBlockListsView.schema'
import type { BlockListEntry } from '@/types/fraud'
import { callsOf, invalid, json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable, stubRoutes } from '../helpers/routes'
import { pickOption } from '../helpers/select'
import { useToastStore } from '@/stores/toast'

/** Request line of the first page of the IP list. */
const LIST_IP = 'GET /admin/fraud/blocklist?page=1&pageSize=20&type=Ip'

/**
 * Builds a block list entry.
 *
 * @param id - Entry id.
 * @param value - The blocked value.
 * @param note - The note, null for none.
 * @returns The entry.
 */
const entry = (id: number, value: string, note: string | null = null): BlockListEntry => ({
  id,
  type: BlockListType.Ip,
  value,
  note,
  createdAt: '2026-11-01T10:00:00Z',
})

/**
 * Wraps entries in the page shape a list endpoint answers with.
 *
 * @param items - The entries of the page.
 * @param totalCount - Total across all pages.
 * @param pageNumber - Page number.
 * @returns The page.
 */
const pageOf = (items: BlockListEntry[], totalCount = items.length, pageNumber = 1): unknown => ({
  items,
  pageNumber,
  pageSize: 20,
  totalCount,
  totalPages: Math.max(1, Math.ceil(totalCount / 20)),
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('FraudBlockListsView', () => {
  it('loads the IP list first and shows its entries', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/blocklist': () => json(pageOf([entry(4, '203.0.113.7', 'spam')])) })
    const { wrapper } = await mountScreen(FraudBlockListsView)

    expect(requestsOf(mock)).toEqual([LIST_IP])
    const table = wrapper.get('[data-testid="fraud-block-list-table"]').text()
    expect(table).toContain('203.0.113.7')
    expect(table).toContain('spam')
    expect(wrapper.get('[data-testid="fraud-block-list-count"]').text()).toContain('1 Records Found')
  })

  it('shows the empty state of a list with no entries', async () => {
    stubRouteTable({ 'GET /admin/fraud/blocklist': () => json(pageOf([])) })
    const { wrapper } = await mountScreen(FraudBlockListsView)

    expect(wrapper.get('[data-testid="fraud-block-list-table"]').text()).toContain('This block list is empty.')
  })

  it('offers a tab per list and loads the list of the tab pressed', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/blocklist': () => json(pageOf([])) })
    const { wrapper } = await mountScreen(FraudBlockListsView)

    expect(wrapper.findAll('[role="tab"]').map((tab) => tab.text())).toEqual(['IP address', 'IP range (CIDR)', 'Email domain', 'Country'])

    await wrapper.get('[data-testid="fraud-block-list-tab-Country"]').trigger('click')
    await flushPromises()

    expect(requestsOf(mock)).toContain('GET /admin/fraud/blocklist?page=1&pageSize=20&type=Country')
    expect(wrapper.get('[data-testid="fraud-block-list-tab-Country"]').attributes('aria-selected')).toBe('true')
  })

  it('adds a typed value with its note and reads the list again', async () => {
    let listed = pageOf([])
    const mock = stubRouteTable({
      'GET /admin/fraud/blocklist': () => json(listed),
      'POST /admin/fraud/blocklist': () => {
        listed = pageOf([entry(5, '203.0.113.9', 'chargebacks')])
        return json(5, 201)
      },
    })
    const { wrapper } = await mountScreen(FraudBlockListsView)

    await wrapper.get('[data-testid="fraud-block-list-value"]').setValue('  203.0.113.9 ')
    await wrapper.get('[data-testid="fraud-block-list-note"]').setValue('chargebacks')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const post = callsOf(mock).find((call) => call.method === 'POST')
    expect(post?.body).toEqual({ type: 'Ip', value: '203.0.113.9', note: 'chargebacks' })
    expect(requestsOf(mock).slice(-1)).toEqual([LIST_IP])
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Entry added to the block list.'])
    expect(wrapper.get('[data-testid="fraud-block-list-table"]').text()).toContain('203.0.113.9')
    expect((wrapper.get('[data-testid="fraud-block-list-value"]').element as HTMLInputElement).value).toBe('')
    expect((wrapper.get('[data-testid="fraud-block-list-note"]').element as HTMLInputElement).value).toBe('')
  })

  it('adds a country picked from the listbox, with no note', async () => {
    const mock = stubRouteTable({
      'GET /admin/fraud/blocklist': () => json(pageOf([])),
      'POST /admin/fraud/blocklist': () => json(6, 201),
    })
    const { wrapper } = await mountScreen(FraudBlockListsView)

    await wrapper.get('[data-testid="fraud-block-list-tab-Country"]').trigger('click')
    await flushPromises()
    await pickOption(wrapper, 'fraud-block-list-value', 'France')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const post = callsOf(mock).find((call) => call.method === 'POST')
    expect(post?.body).toEqual({ type: 'Country', value: 'FR', note: null })
  })

  it('draws a tab per list and the columns of the schema, and keeps the tabs the members of the block list type', async () => {
    stubRouteTable({ 'GET /admin/fraud/blocklist': () => json(pageOf([entry(4, '203.0.113.7')])) })
    const { wrapper } = await mountScreen(FraudBlockListsView)
    await flushPromises()

    expect(BLOCK_LIST_TABS_PART.tabs.map((tab) => tab.key)).toEqual(Object.values(BlockListType))
    for (const tab of BLOCK_LIST_TABS_PART.tabs) {
      expect(wrapper.find(`[data-testid="fraud-block-list-tab-${tab.key}"]`).exists()).toBe(true)
    }
    expect(wrapper.get('[data-testid="fraud-block-list-table"]').findAll('[role="columnheader"]')).toHaveLength(BLOCK_LIST_TABLE.columns.length)
    expect(wrapper.find('[data-testid="ui-form-section-add"]').exists()).toBe(true)
  })

  it('shows a refused value under its field and not in the banner', async () => {
    stubRouteTable({
      'GET /admin/fraud/blocklist': () => json(pageOf([])),
      'POST /admin/fraud/blocklist': () => invalid('The value is not valid.', { Value: ['Enter a valid IP address.'] }),
    })
    const { wrapper } = await mountScreen(FraudBlockListsView)

    await wrapper.get('[data-testid="fraud-block-list-value"]').setValue('nope')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="ui-form-field-value-error"]').text()).toBe('Enter a valid IP address.')
    expect(wrapper.find('[data-testid="fraud-block-list-result"]').exists()).toBe(false)
    expect((wrapper.get('[data-testid="fraud-block-list-value"]').element as HTMLInputElement).value).toBe('nope')
  })

  it('shows a refusal that names no field in the banner', async () => {
    stubRouteTable({
      'GET /admin/fraud/blocklist': () => json(pageOf([])),
      'POST /admin/fraud/blocklist': () => problem('That entry already exists.', 409),
    })
    const { wrapper } = await mountScreen(FraudBlockListsView)

    await wrapper.get('[data-testid="fraud-block-list-value"]').setValue('203.0.113.7')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="fraud-block-list-result"]').text()).toBe('That entry already exists.')
  })

  it('forgets a refusal when another list is chosen', async () => {
    stubRouteTable({
      'GET /admin/fraud/blocklist': () => json(pageOf([])),
      'POST /admin/fraud/blocklist': () => invalid('The value is not valid.', { Value: ['Enter a valid IP address.'] }),
    })
    const { wrapper } = await mountScreen(FraudBlockListsView)
    await wrapper.get('[data-testid="fraud-block-list-value"]').setValue('nope')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    await wrapper.get('[data-testid="fraud-block-list-tab-EmailDomain"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-form-field-value-error"]').exists()).toBe(false)
  })

  it('asks before deleting, then deletes and reads the list again', async () => {
    let listed = pageOf([entry(4, '203.0.113.7')])
    const mock = stubRouteTable({
      'GET /admin/fraud/blocklist': () => json(listed),
      'DELETE /admin/fraud/blocklist/4': () => {
        listed = pageOf([])
        return new Response(null, { status: 204 })
      },
    })
    const { wrapper } = await mountScreen(FraudBlockListsView)

    await wrapper.get('[data-testid="fraud-block-list-delete-4"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('203.0.113.7 will no longer be flagged by the fraud check.')
    expect(requestsOf(mock)).not.toContain('DELETE /admin/fraud/blocklist/4')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(requestsOf(mock)).toContain('DELETE /admin/fraud/blocklist/4')
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Entry removed from the block list.'])
    expect(wrapper.find('[data-testid="fraud-block-list-row-4"]').exists()).toBe(false)
  })

  it('deletes nothing when the confirmation is cancelled', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/blocklist': () => json(pageOf([entry(4, '203.0.113.7')])) })
    const { wrapper } = await mountScreen(FraudBlockListsView)

    await wrapper.get('[data-testid="fraud-block-list-delete-4"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    expect(requestsOf(mock).some((line) => line.startsWith('DELETE'))).toBe(false)
  })

  it('shows the API sentence when a delete is refused', async () => {
    stubRouteTable({
      'GET /admin/fraud/blocklist': () => json(pageOf([entry(4, '203.0.113.7')])),
      'DELETE /admin/fraud/blocklist/4': () => problem('The entry does not exist.', 404),
    })
    const { wrapper } = await mountScreen(FraudBlockListsView)

    await wrapper.get('[data-testid="fraud-block-list-delete-4"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="fraud-block-list-result"]').text()).toBe('The entry does not exist.')
  })

  it('steps back a page when a delete empties the last one', async () => {
    let deleted = false
    const mock = stubRoutes({
      'GET /api/admin/fraud/blocklist?page=1': () => json(pageOf([entry(4, '203.0.113.7')], deleted ? 20 : 21, 1)),
      'GET /api/admin/fraud/blocklist?page=2': () => json(pageOf(deleted ? [] : [entry(9, '203.0.113.9')], deleted ? 20 : 21, 2)),
      'DELETE /api/admin/fraud/blocklist/9': () => {
        deleted = true
        return new Response(null, { status: 204 })
      },
    })
    const { wrapper } = await mountScreen(FraudBlockListsView)

    await wrapper.get('[data-testid="fraud-block-list-pagination-page-2"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="fraud-block-list-row-9"]').exists()).toBe(true)

    await wrapper.get('[data-testid="fraud-block-list-delete-9"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(requestsOf(mock).slice(-3)).toEqual([
      'DELETE /admin/fraud/blocklist/9',
      'GET /admin/fraud/blocklist?page=2&pageSize=20&type=Ip',
      'GET /admin/fraud/blocklist?page=1&pageSize=20&type=Ip',
    ])
    expect(wrapper.find('[data-testid="fraud-block-list-row-4"]').exists()).toBe(true)
  })

  it('opens the list the address names and marks its tab', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/blocklist': () => json(pageOf([])) })
    const { wrapper } = await mountScreen(FraudBlockListsView, {}, '/?type=Country')

    expect(requestsOf(mock)).toEqual(['GET /admin/fraud/blocklist?page=1&pageSize=20&type=Country'])
    expect(wrapper.get('[data-testid="fraud-block-list-tab-Country"]').attributes('aria-selected')).toBe('true')
  })

  it('shows the IP list when the address names a type that is not one', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/blocklist': () => json(pageOf([])) })
    const { wrapper } = await mountScreen(FraudBlockListsView, {}, '/?type=Nonsense')

    expect(requestsOf(mock)).toEqual([LIST_IP])
    expect(wrapper.get('[data-testid="fraud-block-list-tab-Ip"]').attributes('aria-selected')).toBe('true')
  })

  it('puts the typed search in the address and sends it with the list on show after the pause', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/blocklist': () => json(pageOf([entry(4, '203.0.113.7')])) })
    const { wrapper, router } = await mountScreen(FraudBlockListsView)

    await wrapper.get('[data-testid="fraud-block-list-search"]').setValue('203.0')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: '203.0' })
    expect(requestsOf(mock).slice(-1)).toEqual(['GET /admin/fraud/blocklist?page=1&pageSize=20&type=Ip&search=203.0'])
  })

  it('restores the search from the address and sends it', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/blocklist': () => json(pageOf([entry(4, '203.0.113.7')])) })
    const { wrapper } = await mountScreen(FraudBlockListsView, {}, '/?type=EmailDomain&search=spam')

    expect(requestsOf(mock)).toEqual(['GET /admin/fraud/blocklist?page=1&pageSize=20&type=EmailDomain&search=spam'])
    expect((wrapper.get('[data-testid="fraud-block-list-search"]').element as HTMLInputElement).value).toBe('spam')
  })

  it('names the search in the empty state of a searched list', async () => {
    stubRouteTable({ 'GET /admin/fraud/blocklist': () => json(pageOf([])) })
    const { wrapper } = await mountScreen(FraudBlockListsView, {}, '/?search=nothing')

    expect(wrapper.get('[data-testid="fraud-block-list-table"]').text()).toContain('No entries match your search.')
  })

  it('starts the chosen list from its first page with an empty search, loading it once', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/blocklist': () => json(pageOf([entry(4, '203.0.113.7')], 45)) })
    const { wrapper, router } = await mountScreen(FraudBlockListsView, {}, '/?search=spam&page=2')
    const before = requestsOf(mock).length

    await wrapper.get('[data-testid="fraud-block-list-tab-Country"]').trigger('click')
    await flushPromises()
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ type: 'Country' })
    expect(requestsOf(mock).slice(before)).toEqual(['GET /admin/fraud/blocklist?page=1&pageSize=20&type=Country'])
    expect((wrapper.get('[data-testid="fraud-block-list-search"]').element as HTMLInputElement).value).toBe('')
  })

  it('keeps the list and the search when another page is opened', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/blocklist': () => json(pageOf([entry(4, '203.0.113.7')], 45)) })
    const { wrapper, router } = await mountScreen(FraudBlockListsView, {}, '/?type=Cidr&search=203')

    await wrapper.get('[data-testid="fraud-block-list-pagination-page-2"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ type: 'Cidr', search: '203', page: '2' })
    expect(requestsOf(mock).slice(-1)).toEqual(['GET /admin/fraud/blocklist?page=2&pageSize=20&type=Cidr&search=203'])
  })

  it('goes back to the first page after an add made from a later page', async () => {
    const mock = stubRouteTable({
      'GET /admin/fraud/blocklist': () => json(pageOf([entry(4, '203.0.113.7')], 45)),
      'POST /admin/fraud/blocklist': () => json(5, 201),
    })
    const { wrapper, router } = await mountScreen(FraudBlockListsView, {}, '/?page=2')

    await wrapper.get('[data-testid="fraud-block-list-value"]').setValue('203.0.113.9')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({})
    expect(requestsOf(mock).slice(-1)).toEqual([LIST_IP])
  })

  it('loads again from the Retry of a refused load', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/blocklist': () => problem('Not allowed.', 403) })
    const { wrapper } = await mountScreen(FraudBlockListsView)
    const before = mock.mock.calls.length

    expect(wrapper.get('[data-testid="fraud-block-list-error"]').text()).toContain('Not allowed.')
    await wrapper.get('[data-testid="fraud-block-list-error-retry"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.length).toBeGreaterThan(before)
  })
})
