/**
 * Mount tests for SupportTicketsView: the list, the search, the status, priority, department and flag filters kept in
 * the address and sent to the API, their removable chips, the bulk bar and the empty and error states, through the real
 * store and transport with `fetch` stubbed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import SupportTicketsView from '@/views/SupportTicketsView.vue'
import schema from '@/views/SupportTicketsView.schema'
import { renderedCaptions, schemaCaptions } from '../helpers/schemaTable'
import { TicketFlagFilter, TicketPriority, TicketStatus } from '@/types/ticket'
import { useToastStore } from '@/stores/toast'
import { mountRouted } from '../helpers/mount'
import { json, problem, stubFetch, findCall } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'
import { toastMessages } from '../helpers/toasts'

/** One ticket row as the list endpoint returns it. */
const ticket = {
  id: 5,
  subject: 'DNS is down',
  status: TicketStatus.AwaitingReply,
  priority: TicketPriority.High,
  createdAt: '2026-10-01T00:00:00Z',
  replyCount: 1,
  departmentName: 'Support',
  lastReplyAt: null,
  isFlagged: false,
  clientId: 3,
  clientName: 'Ada Lovelace',
}

/** The support departments the department filter offers. */
const departments = [
  { id: 1, name: 'Support', email: 'support@example.test' },
  { id: 2, name: 'Billing', email: 'billing@example.test' },
]

/**
 * Stubs the endpoints the list calls.
 *
 * @param items - The tickets the list answers with.
 * @param totalCount - The total the list answers with.
 * @returns The mock, for asserting the requests made.
 */
const stubList = (items: unknown[] = [ticket], totalCount = items.length): ReturnType<typeof vi.fn> =>
  stubRoutes({
    'GET /api/tickets/departments': () => json(departments),
    'GET /api/tickets': () => json({ items, totalCount }),
    'POST /api/tickets/bulk': () => json(null),
  })

/**
 * Mounts the list at an address and waits for its first load.
 *
 * @param url - The address to open, with its query.
 * @returns The wrapper and the router.
 */
const mountListAt = async (url = '/support/tickets') => {
  const screen = await mountRouted(SupportTicketsView, '/support/tickets', url)
  await flushPromises()
  return screen
}

/**
 * Lists the requests the mock has seen for the ticket list.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/tickets?'))

/**
 * Opens the filter dialog, chooses a value in one of its selects and applies it.
 *
 * @param wrapper - The mounted list.
 * @param testid - The `data-testid` of the select to use.
 * @param value - The option value to choose.
 */
const applyFilter = async (wrapper: VueWrapper, testid: string, value: string) => {
  await wrapper.get('[data-testid="support-tickets-filter"]').trigger('click')
  await flushPromises()
  wrapper
    .findAllComponents(UiSelect)
    .find((select) => select.attributes('data-testid') === testid)
    ?.vm.$emit('update:modelValue', value)
  await flushPromises()
  await wrapper.get('[data-testid="filter-apply"]').trigger('click')
  await flushPromises()
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('SupportTicketsView', () => {
  it('draws the table head of its schema columns, in order', async () => {
    stubList()

    const { wrapper } = await mountListAt()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('lists the tickets with subject, department and translated status', async () => {
    stubList()

    const { wrapper } = await mountListAt()
    const row = wrapper.get('[data-testid="support-tickets-row-5"]')

    expect(row.text()).toContain('DNS is down')
    expect(row.text()).toContain('Support')
    expect(row.text()).toContain('Awaiting Reply')
    expect(wrapper.findAll('th').find((header) => header.text() === 'Subject')?.classes()).toEqual(expect.arrayContaining(['w-[28%]', 'max-w-0', 'truncate']))
  })

  it('shows the priority of each ticket beside its status', async () => {
    stubList()

    const { wrapper } = await mountListAt()

    expect(wrapper.get('[data-testid="support-tickets-row-5"]').text()).toContain('High')
  })

  it('names the client who opened the ticket', async () => {
    stubList()

    const { wrapper } = await mountListAt()

    expect(wrapper.get('[data-testid="support-tickets-row-5"]').text()).toContain('Ada Lovelace')
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="support-tickets-search"]').setValue('dns')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'dns' })
    expect(listUrls(mock).at(-1)).toBe('/api/tickets?page=1&pageSize=20&search=dns')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubList([ticket], 45)
    const { wrapper, router } = await mountListAt()

    expect(wrapper.find('[data-testid="ui-card-footer"] [data-testid="support-tickets-pagination-next"]').exists()).toBe(true)

    await wrapper.get('[data-testid="support-tickets-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/tickets?page=2&pageSize=20')
  })

  it('sends the status picked in the filter dialog on Apply and shows it as a chip', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountListAt()

    await applyFilter(wrapper, 'support-tickets-filter-status', TicketStatus.Closed)

    expect(router.currentRoute.value.query).toEqual({ status: 'Closed' })
    expect(listUrls(mock).at(-1)).toBe('/api/tickets?page=1&pageSize=20&status=Closed')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Closed')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('1')
  })

  it('sends the priority picked in the filter dialog on Apply and shows it as a chip', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountListAt()

    await applyFilter(wrapper, 'support-tickets-filter-priority', TicketPriority.Low)

    expect(router.currentRoute.value.query).toEqual({ priority: 'Low' })
    expect(listUrls(mock).at(-1)).toBe('/api/tickets?page=1&pageSize=20&priority=Low')
    expect(wrapper.get('[data-testid="filter-chip-priority"]').text()).toContain('Low')
  })

  it('offers the support departments and sends the one picked, named on its chip', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountListAt()
    await wrapper.get('[data-testid="support-tickets-filter"]').trigger('click')
    await flushPromises()
    const select = wrapper.findAllComponents(UiSelect).find((candidate) => candidate.attributes('data-testid') === 'support-tickets-filter-department')
    if (select === undefined) {
      throw new Error('The department select is missing')
    }
    expect((select.props() as unknown as { options: unknown[] }).options).toEqual(expect.arrayContaining([{ value: '2', label: 'Billing' }]))

    await applyFilter(wrapper, 'support-tickets-filter-department', '2')

    expect(router.currentRoute.value.query).toEqual({ departmentId: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/tickets?page=1&pageSize=20&departmentId=2')
    expect(wrapper.get('[data-testid="filter-chip-departmentId"]').text()).toContain('Billing')
  })

  it('sends the flagged choice picked in the filter dialog as a flag, not as a status', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountListAt()

    await applyFilter(wrapper, 'support-tickets-filter-flagged', TicketFlagFilter.Flagged)

    expect(router.currentRoute.value.query).toEqual({ flagged: 'flagged' })
    expect(listUrls(mock).at(-1)).toBe('/api/tickets?page=1&pageSize=20&flagged=true')
    expect(wrapper.get('[data-testid="filter-chip-flagged"]').text()).toContain('Flagged')
  })

  it('restores the filters, the search and the page from the address and sends them together', async () => {
    const mock = stubList([ticket], 45)
    const { wrapper } = await mountListAt('/support/tickets?search=dns&status=AwaitingReply&priority=High&departmentId=2&flagged=flagged&page=2')

    expect(listUrls(mock)[0]).toBe('/api/tickets?page=2&pageSize=20&search=dns&status=AwaitingReply&priority=High&departmentId=2&flagged=true')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Awaiting Reply')
    expect(wrapper.get('[data-testid="filter-chip-priority"]').text()).toContain('High')
    expect(wrapper.get('[data-testid="filter-chip-departmentId"]').text()).toContain('Billing')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('4')
    expect((wrapper.get('[data-testid="support-tickets-search"]').element as HTMLInputElement).value).toBe('dns')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubList()
    const { wrapper } = await mountListAt('/support/tickets?status=Answered&priority=Urgent&departmentId=abc&flagged=yes')

    expect(listUrls(mock)[0]).toBe('/api/tickets?page=1&pageSize=20')
    expect(wrapper.find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubList()
    const { wrapper, router } = await mountListAt('/support/tickets?status=Closed&priority=High')

    await wrapper.get('[data-testid="filter-chip-remove-status"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ priority: 'High' })
    expect(listUrls(mock).at(-1)).toBe('/api/tickets?page=1&pageSize=20&priority=High')
  })

  it('hides the flag of an unflagged ticket until hover and fills it with the warning colour once flagged', async () => {
    stubList([ticket, { ...ticket, id: 6, isFlagged: true }])
    const { wrapper } = await mountListAt()

    const unflagged = wrapper.get('[data-testid="support-tickets-flag-5"]')
    expect(unflagged.classes()).toEqual(expect.arrayContaining(['opacity-0', 'text-text-muted']))
    expect(unflagged.attributes('aria-label')).toBe('Flag')
    const flagged = wrapper.get('[data-testid="support-tickets-flag-6"]')
    expect(flagged.classes()).toContain('text-status-yellow')
    expect(flagged.classes()).not.toContain('opacity-0')
    expect(flagged.attributes('aria-label')).toBe('Unflag')
  })

  it('says "No replies yet" for an unanswered ticket and a relative date for an answered one', async () => {
    stubList([ticket, { ...ticket, id: 6, lastReplyAt: new Date(Date.now() - 3 * 3600 * 1000).toISOString() }])
    const { wrapper } = await mountListAt()

    expect(wrapper.get('[data-testid="support-tickets-row-5"]').text()).toContain('No replies yet')
    expect(wrapper.get('[data-testid="support-tickets-row-6"]').text()).toContain('3 hours ago')
  })

  it('shows the bulk bar for a selection and sends the chosen action', async () => {
    const mock = stubList()
    const { wrapper } = await mountListAt()
    expect(wrapper.find('[data-testid="support-tickets-bulk"]').exists()).toBe(false)

    await wrapper.get('[data-testid="support-tickets-select-5"]').trigger('click')
    await wrapper.get('[data-testid="support-tickets-bulk-close"]').trigger('click')
    await flushPromises()

    expect(findCall(mock, 'POST', '/api/tickets/bulk')?.body).toEqual({ ticketIds: [5], action: 'close' })
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Changes saved successfully.'])
  })

  it('deletes the selection only after the danger confirmation', async () => {
    const mock = stubList()
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="support-tickets-select-5"]').trigger('click')
    await wrapper.get('[data-testid="support-tickets-bulk-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()
    expect(findCall(mock, 'POST', '/api/tickets/bulk')).toBeUndefined()

    await wrapper.get('[data-testid="support-tickets-bulk-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(findCall(mock, 'POST', '/api/tickets/bulk')?.body).toEqual({ ticketIds: [5], action: 'delete' })
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Record deleted.'])
  })

  it('shows the API sentence when a bulk action is refused', async () => {
    stubRoutes({
      'GET /api/tickets/departments': () => json(departments),
      'POST /api/tickets/bulk': () => problem('Tickets are locked.', 409),
      'GET /api/tickets': () => json({ items: [ticket], totalCount: 1 }),
    })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="support-tickets-select-5"]').trigger('click')
    await wrapper.get('[data-testid="support-tickets-bulk-close"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toContain('Tickets are locked.')
    expect(wrapper.find('[data-testid="support-tickets-error"]').exists()).toBe(false)
  })

  it('shows the empty state, and the filter hint when the list is narrowed', async () => {
    stubList([])
    const { wrapper } = await mountListAt()

    expect(wrapper.get('[data-testid="support-tickets-empty"]').text()).toContain('No tickets found')
    expect(wrapper.find('[data-testid="support-tickets-clear-empty"]').exists()).toBe(false)

    const searched = (await mountListAt('/support/tickets?search=zzz')).wrapper
    expect(searched.get('[data-testid="support-tickets-empty"]').text()).toContain('No tickets match your filters')
    expect(searched.find('[data-testid="support-tickets-clear-empty"]').exists()).toBe(false)
  })

  it('clears the filters from the empty state and keeps the search', async () => {
    stubList([])
    const { wrapper, router } = await mountListAt('/support/tickets?status=Closed&search=zzz')

    await wrapper.get('[data-testid="support-tickets-clear-empty"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'zzz' })
  })

  it('shows the API sentence when the list is refused', async () => {
    stubFetch(problem('Not allowed.', 403))

    const { wrapper } = await mountListAt()

    expect(wrapper.get('[data-testid="support-tickets-error"]').text()).toBe('Not allowed.')
  })

  it('loads the list again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/tickets/departments': () => json(departments),
      'GET /api/tickets': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json({ items: [ticket], totalCount: 1 })
      },
    })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="support-tickets-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="support-tickets-error"]').exists()).toBe(false)
  })
})
