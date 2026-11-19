/**
 * Smoke tests for EmailCampaignsListView: the list with status badges, dates and delivery counters, the subject
 * search, the status filter and the page kept in the address and sent to the API, each applied filter as a removable
 * chip, delete offered for drafts only and confirmed first, and the empty and error states (with Retry), through the
 * real store and transport with `fetch` stubbed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import EmailCampaignsListView from '@/views/EmailCampaignsListView.vue'
import { CampaignStatus } from '@/types/emailcampaign'
import { json, problem } from '../helpers/fetch'
import { applyListFilter } from '../helpers/listFilter'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { renderedCaptions, schemaCaptions, stubPendingFetch } from '../helpers/schemaTable'
import schema from '@/views/EmailCampaignsListView.schema'
import { useToastStore } from '@/stores/toast'

/** The address the screen is registered under. */
const PATH = '/support/email-campaigns'

/** The delivery counters of a campaign nothing has been sent for. */
const untouched = { total: 0, pending: 0, sent: 0, failed: 0, unsubscribed: 0, opened: 0, uniqueOpened: 0, clicked: 0, uniqueClicked: 0, openRate: 0, clickRate: 0 }

/** A draft as the list endpoint returns it. */
const draft = {
  id: 3,
  subject: 'Spring offer',
  status: CampaignStatus.Draft,
  scheduledAt: null,
  sentAt: null,
  createdAt: '2026-10-01T10:30:00Z',
  stats: untouched,
}

/** A finished campaign as the list endpoint returns it. */
const sent = {
  id: 4,
  subject: 'Maintenance notice',
  status: CampaignStatus.Sent,
  scheduledAt: '2026-10-02T08:00:00Z',
  sentAt: '2026-10-02T08:05:00Z',
  createdAt: '2026-10-01T11:00:00Z',
  stats: { total: 40, pending: 0, sent: 36, failed: 1, unsubscribed: 3, opened: 20, uniqueOpened: 15, clicked: 9, uniqueClicked: 6, openRate: 0.4167, clickRate: 0.1667 },
}

/**
 * Wraps rows in the page shape the list endpoint answers with.
 *
 * @param items - The rows of the page.
 * @param totalCount - Matches across all pages; the page length by default.
 * @returns The JSON body.
 */
const page = (items: unknown[], totalCount = items.length) => ({
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
  const screen = await mountRouted(EmailCampaignsListView, PATH, url)
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
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/admin/email-campaigns?'))

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('EmailCampaignsListView', () => {
  it('lists the campaigns with status, subject and delivery counters', async () => {
    stubRoutes({ 'GET /api/admin/email-campaigns?': () => json(page([draft, sent])) })

    const { wrapper } = await mountListAt()

    expect(wrapper.get('[data-testid="email-campaigns-row-3"]').text()).toContain('Spring offer')
    expect(wrapper.get('[data-testid="email-campaigns-row-3"]').text()).toContain('Draft')
    const row = wrapper.get('[data-testid="email-campaigns-row-4"]').text()
    expect(row).toContain('Maintenance notice')
    expect(row).toContain('Sent')
    expect(row).toContain('36 sent, 1 failed, 3 unsubscribed')
    expect(wrapper.text()).toContain('Campaigns: 2')
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/admin/email-campaigns?': () => json(page([draft])) })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="email-campaigns-search"]').setValue('spring')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'spring' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/email-campaigns?page=1&pageSize=20&search=spring')
  })

  it('restores the search, the status and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ 'GET /api/admin/email-campaigns?': () => json(page([draft], 45)) })
    const { wrapper } = await mountListAt(`${PATH}?search=spring&status=Draft&page=2`)

    expect(listUrls(mock)[0]).toBe('/api/admin/email-campaigns?page=2&pageSize=20&search=spring&status=Draft')
    expect((wrapper.get('[data-testid="email-campaigns-search"]').element as HTMLInputElement).value).toBe('spring')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Draft')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('1')
  })

  it('keeps the chosen page in the address', async () => {
    stubRoutes({ 'GET /api/admin/email-campaigns?': () => json(page([draft], 45)) })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="email-campaigns-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
  })

  it('sends the status picked in the filter dialog on Apply and shows it as a chip', async () => {
    const mock = stubRoutes({ 'GET /api/admin/email-campaigns?': () => json(page([sent])) })
    const { wrapper, router } = await mountListAt()

    await applyListFilter(wrapper, { 'email-campaigns-filter-status': CampaignStatus.Sent })

    expect(router.currentRoute.value.query).toEqual({ status: 'Sent' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/email-campaigns?page=1&pageSize=20&status=Sent')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Sent')
  })

  it('does not send a status the address holds that is not a known value', async () => {
    const mock = stubRoutes({ 'GET /api/admin/email-campaigns?': () => json(page([draft])) })
    await mountListAt(`${PATH}?status=Nonsense`)

    expect(listUrls(mock)[0]).toBe('/api/admin/email-campaigns?page=1&pageSize=20')
  })

  it('removes the status from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/admin/email-campaigns?': () => json(page([draft])) })
    const { wrapper, router } = await mountListAt(`${PATH}?status=Draft&search=spring`)

    await wrapper.get('[data-testid="filter-chip-remove-status"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'spring' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/email-campaigns?page=1&pageSize=20&search=spring')
  })

  it('offers delete for a draft only', async () => {
    stubRoutes({ 'GET /api/admin/email-campaigns?': () => json(page([draft, sent])) })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="email-campaigns-3-actions"]').trigger('click')
    expect(wrapper.find('[data-testid="email-campaigns-3-delete"]').exists()).toBe(true)

    expect(wrapper.find('[data-testid="email-campaigns-4-delete"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="email-campaigns-4-open"]').text()).toBe('View')
  })

  it('asks for confirmation before deleting, then deletes, reports it and reloads the page', async () => {
    let deleted = false
    const mock = stubRoutes({
      'GET /api/admin/email-campaigns?': () => json(page(deleted ? [] : [draft])),
      'DELETE /api/admin/email-campaigns/3': () => {
        deleted = true
        return new Response(null, { status: 204 })
      },
    })
    const { wrapper } = await mountListAt(`${PATH}?search=spring`)
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)

    await wrapper.get('[data-testid="email-campaigns-3-actions"]').trigger('click')
    await wrapper.get('[data-testid="email-campaigns-3-delete"]').trigger('click')
    expect(wrapper.find('[role="dialog"]').exists()).toBe(true)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url, init]) => url === '/api/admin/email-campaigns/3' && (init as RequestInit).method === 'DELETE')).toBe(true)
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Campaign deleted.'])
    expect(wrapper.find('[data-testid="email-campaigns-row-3"]').exists()).toBe(false)
    expect(listUrls(mock).at(-1)).toBe('/api/admin/email-campaigns?page=1&pageSize=20&search=spring')
  })

  it('steps back a page when the delete empties the last one', async () => {
    let deleted = false
    const mock = stubRoutes({
      'GET /api/admin/email-campaigns?': () => json(page(deleted ? [] : [draft], deleted ? 20 : 21)),
      'DELETE /api/admin/email-campaigns/3': () => {
        deleted = true
        return new Response(null, { status: 204 })
      },
    })
    const { wrapper, router } = await mountListAt(`${PATH}?page=2`)

    await wrapper.get('[data-testid="email-campaigns-3-actions"]').trigger('click')
    await wrapper.get('[data-testid="email-campaigns-3-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.page).toBeUndefined()
    expect(listUrls(mock).at(-1)).toBe('/api/admin/email-campaigns?page=1&pageSize=20')
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/admin/email-campaigns?': () => json(page([draft])) })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="email-campaigns-3-actions"]').trigger('click')
    await wrapper.get('[data-testid="email-campaigns-3-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
  })

  it('shows the API sentence when the delete is refused and keeps the list', async () => {
    stubRoutes({
      'GET /api/admin/email-campaigns?': () => json(page([draft])),
      'DELETE /api/admin/email-campaigns/3': () => problem('Only a draft can be deleted.', 409),
    })
    const { wrapper } = await mountListAt()

    await wrapper.get('[data-testid="email-campaigns-3-actions"]').trigger('click')
    await wrapper.get('[data-testid="email-campaigns-3-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="email-campaigns-feedback"]').text()).toBe('Only a draft can be deleted.')
    expect(wrapper.find('[data-testid="email-campaigns-row-3"]').exists()).toBe(true)
  })

  it('shows the empty state, and the filter hint when the list is narrowed', async () => {
    stubRoutes({ 'GET /api/admin/email-campaigns?': () => json(page([])) })
    const { wrapper } = await mountListAt()

    expect(wrapper.get('[data-testid="email-campaigns-empty"]').text()).toContain('No campaigns yet')
    expect(wrapper.find('[data-testid="email-campaigns-clear-empty"]').exists()).toBe(false)

    const narrowed = (await mountListAt(`${PATH}?search=zzz&status=Sent`)).wrapper
    expect(narrowed.get('[data-testid="email-campaigns-empty"]').text()).toContain('No campaigns match your filters')
    expect(narrowed.find('[data-testid="email-campaigns-clear-empty"]').exists()).toBe(true)
  })

  it('clears the filters from the empty state and keeps the search', async () => {
    stubRoutes({ 'GET /api/admin/email-campaigns?': () => json(page([])) })
    const { wrapper, router } = await mountListAt(`${PATH}?status=Sent&search=zzz`)

    await wrapper.get('[data-testid="email-campaigns-clear-empty"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'zzz' })
  })

  it('shows the API sentence when the list is refused, and reloads from the Retry', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/admin/email-campaigns?': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json(page([draft]))
      },
    })
    const { wrapper } = await mountListAt()
    expect(wrapper.get('[data-testid="email-campaigns-error"]').text()).toBe('Not allowed.')

    await wrapper.get('[data-testid="email-campaigns-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="email-campaigns-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="email-campaigns-row-3"]').exists()).toBe(true)
  })

  it('links the add button to the new campaign form', async () => {
    stubRoutes({ 'GET /api/admin/email-campaigns?': () => json(page([])) })

    const { wrapper } = await mountListAt()

    expect(wrapper.get('[data-testid="email-campaigns-add"]').attributes('href')).toBe('/support/email-campaigns/new')
  })

  it('draws the table head from the schema columns while the first load is in flight', async () => {
    stubPendingFetch()

    const { wrapper } = await mountListAt()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })
})
