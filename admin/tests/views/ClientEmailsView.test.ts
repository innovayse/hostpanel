/**
 * Mount tests for ClientEmailsView: it renders against the real kit, settles on an API refusal, captions each send
 * outcome, and the search, the outcome, the period and the page are kept in the address, sent to the API (which narrows
 * the entries before it pages) and shown as removable chips.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import ClientEmailsView from '@/views/ClientEmailsView.vue'
import { mountClientView } from '../helpers/clientViews'
import { json, stubFetch } from '../helpers/fetch'
import { applyListFilter } from '../helpers/listFilter'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { flushPromises } from '@vue/test-utils'
import { EmailSendFailure } from '@/types/emaillog'
import type { EmailLog } from '@/types/emaillog'
import type { PaginatedList } from '@/types/paginatedlist'

/** One delivered entry. */
const delivered: EmailLog = { id: 1, to: 'a@example.test', subject: 'Welcome', sentAt: '2026-10-01T08:00:00Z', success: true, failure: null }

/** One failed entry. */
const failed: EmailLog = {
  id: 2,
  to: 'a@example.test',
  subject: 'Invoice',
  sentAt: '2026-10-02T08:00:00Z',
  success: false,
  failure: EmailSendFailure.RecipientRejected,
}

/**
 * Builds a page of the log.
 *
 * @param items - The entries of the page.
 * @param totalCount - The total across all pages.
 * @returns The page as the API answers it.
 */
const pageOf = (items: EmailLog[], totalCount = items.length): PaginatedList<EmailLog> => ({
  items,
  pageNumber: 1,
  pageSize: 20,
  totalCount,
  totalPages: Math.max(1, Math.ceil(totalCount / 20)),
})

/**
 * Mounts the screen for client 7 at an address and waits for its first load.
 *
 * @param query - The query string to open, with its leading question mark or empty.
 * @returns The wrapper and the router.
 */
const mountEmailsAt = async (query = '') => {
  const screen = await mountRouted(ClientEmailsView, '/clients/:id/emails', `/clients/7/emails${query}`)
  await flushPromises()
  return screen
}

/**
 * Lists the requests the mock has seen for the e-mails of the client.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/admin/email-logs/client/7?'))

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientEmailsView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientEmailsView)

    expect(wrapper.exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientEmailsView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('loads again from the Retry of a refused load', async () => {
    const wrapper = await mountClientView(ClientEmailsView)
    await flushPromises()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="client-emails-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })

  it('captions each entry as delivered or with its translated failure reason', async () => {
    const wrapper = await mountClientView(ClientEmailsView)
    stubFetch(json(pageOf([delivered, failed])))

    await wrapper.get('[data-testid="client-emails-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-emails-outcome-1"]').text()).toBe('Delivered')
    expect(wrapper.get('[data-testid="client-emails-outcome-2"]').text()).toBe('Recipient address rejected')
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/admin/email-logs/client/7': () => json(pageOf([delivered])) })
    const { wrapper, router } = await mountEmailsAt()

    await wrapper.get('[data-testid="client-emails-search"]').setValue('invoice')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'invoice' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/email-logs/client/7?page=1&pageSize=20&search=invoice')
  })

  it('sends the outcome picked in the filter dialog on Apply as the success switch and shows it as a chip', async () => {
    const mock = stubRoutes({ 'GET /api/admin/email-logs/client/7': () => json(pageOf([failed])) })
    const { wrapper, router } = await mountEmailsAt()

    await applyListFilter(wrapper, { 'client-emails-filter-outcome': 'failed' }, 'client-emails-filter')

    expect(router.currentRoute.value.query).toEqual({ outcome: 'failed' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/email-logs/client/7?page=1&pageSize=20&success=false')
    expect(wrapper.get('[data-testid="filter-chip-outcome"]').text()).toContain('Not delivered')
  })

  it('restores the search, the outcome, the period and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ 'GET /api/admin/email-logs/client/7': () => json(pageOf([delivered])) })
    const { wrapper } = await mountEmailsAt('?search=welcome&outcome=delivered&from=2026-03-01&to=2026-03-31&page=2')

    expect(listUrls(mock)[0]).toBe(
      '/api/admin/email-logs/client/7?page=2&pageSize=20&search=welcome&success=true&from=2026-03-01&to=2026-03-31',
    )
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubRoutes({ 'GET /api/admin/email-logs/client/7': () => json(pageOf([delivered])) })
    const { wrapper } = await mountEmailsAt('?outcome=maybe&from=soon')

    expect(listUrls(mock)[0]).toBe('/api/admin/email-logs/client/7?page=1&pageSize=20')
    expect(wrapper.find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/admin/email-logs/client/7': () => json(pageOf([delivered])) })
    const { wrapper, router } = await mountEmailsAt('?outcome=delivered&from=2026-03-01')

    await wrapper.get('[data-testid="filter-chip-remove-outcome"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ from: '2026-03-01' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/email-logs/client/7?page=1&pageSize=20&from=2026-03-01')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubRoutes({ 'GET /api/admin/email-logs/client/7': () => json(pageOf([delivered], 60)) })
    const { wrapper, router } = await mountEmailsAt()

    await wrapper.get('[data-testid="client-emails-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/email-logs/client/7?page=2&pageSize=20')
  })
})
