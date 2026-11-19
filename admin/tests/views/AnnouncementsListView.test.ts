/**
 * Mount tests for AnnouncementsListView: the list, the search, the published filter and the page kept in the address
 * and sent to the API, the delete confirmation and its outcome, and the empty and error states (with Retry), through
 * the real store and transport with `fetch` stubbed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import AnnouncementsListView from '@/views/AnnouncementsListView.vue'
import schema from '@/views/AnnouncementsListView.schema'
import { renderedCaptions, schemaCaptions } from '../helpers/schemaTable'
import { applyListFilter } from '../helpers/listFilter'
import { mountRouted, mountView } from '../helpers/mount'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { stubPathRoutes, stubRoutes } from '../helpers/routes'
import { useToastStore } from '@/stores/toast'

/** One announcement as the list endpoint returns it. */
const announcement = { id: 3, title: 'Maintenance window', content: 'Tonight', isPublished: true, createdAt: '2026-10-01T10:30:00Z' }

/** The one-announcement list. */
const list = () => json({ items: [announcement], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 })

/**
 * Mounts the screen at an address and waits for its first load.
 *
 * @param query - The query string to open, with its leading question mark or empty.
 * @returns The wrapper and the router.
 */
const mountListAt = async (query = '') => {
  const screen = await mountRouted(AnnouncementsListView, '/support/announcements', `/support/announcements${query}`)
  await flushPromises()
  return screen
}

/**
 * Lists the requests the mock has seen for the list endpoint.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/announcements?'))

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('AnnouncementsListView', () => {
  it('draws the table head of its schema columns, in order', async () => {
    stubPathRoutes([{ path: '/announcements', body: { items: [announcement], totalCount: 1 } }])

    const wrapper = await mountView(AnnouncementsListView)

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('lists the announcements with their published state', async () => {
    stubPathRoutes([{ path: '/announcements', body: { items: [announcement], totalCount: 1 } }])

    const wrapper = await mountView(AnnouncementsListView)
    const row = wrapper.get('[data-testid="announcements-row-3"]')

    expect(row.text()).toContain('Maintenance window')
    expect(row.get('[role="img"]').attributes('aria-label')).toBe('Yes')
    expect(wrapper.findAll('th').some((header) => header.classes().includes('max-w-0'))).toBe(true)
    expect(wrapper.get('[data-testid="announcements-count"]').text()).toContain('1')
  })

  it('asks for confirmation before deleting, then deletes and reloads', async () => {
    const mock = stubPathRoutes([{ path: '/announcements', body: { items: [announcement], totalCount: 1 } }])
    const wrapper = await mountView(AnnouncementsListView)
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)

    await wrapper.get('[data-testid="announcements-3-actions"]').trigger('click')

    await wrapper.get('[data-testid="announcements-3-delete"]').trigger('click')
    expect(wrapper.find('[role="dialog"]').exists()).toBe(true)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(callOf(mock, 1)).toMatchObject({ url: '/api/announcements/3', method: 'DELETE' })
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
  })

  it('shows the empty state when there are no announcements', async () => {
    stubPathRoutes([{ path: '/announcements', body: { items: [], totalCount: 0 } }])

    const wrapper = await mountView(AnnouncementsListView)

    expect(wrapper.get('[data-testid="announcements-table"]').text()).toContain('No announcements found')
  })

  it('shows the API sentence when the list is refused', async () => {
    stubFetch(problem('Not allowed.', 403))

    const wrapper = await mountView(AnnouncementsListView)

    expect(wrapper.get('[data-testid="announcements-error"]').text()).toBe('Not allowed.')
  })

  it('reports a successful delete next to the list', async () => {
    const list = { items: [announcement], totalCount: 1 }
    stubFetch(json(list), new Response(null, { status: 204 }), json(list))
    const wrapper = await mountView(AnnouncementsListView)

    await wrapper.get('[data-testid="announcements-3-actions"]').trigger('click')

    await wrapper.get('[data-testid="announcements-3-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Announcement deleted.'])
  })

  it('shows the API sentence when the delete is refused and keeps the list', async () => {
    stubFetch(json({ items: [announcement], totalCount: 1 }), problem('Announcement is locked.', 409))
    const wrapper = await mountView(AnnouncementsListView)

    await wrapper.get('[data-testid="announcements-3-actions"]').trigger('click')

    await wrapper.get('[data-testid="announcements-3-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="announcements-feedback"]').text()).toBe('Announcement is locked.')
    expect(wrapper.find('[data-testid="announcements-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="announcements-row-3"]').exists()).toBe(true)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubFetch(json({ items: [announcement], totalCount: 1 }))
    const wrapper = await mountView(AnnouncementsListView)

    await wrapper.get('[data-testid="announcements-3-actions"]').trigger('click')

    await wrapper.get('[data-testid="announcements-3-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
  })

  it('reloads the list from the Retry of a refused load', async () => {
    stubFetch(problem('Not allowed.', 403), json({ items: [announcement], totalCount: 1 }))
    const wrapper = await mountView(AnnouncementsListView)

    await wrapper.get('[data-testid="announcements-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="announcements-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="announcements-row-3"]').exists()).toBe(true)
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/announcements': list })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="announcements-search"]').setValue('window')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'window' })
    expect(listUrls(mock).at(-1)).toBe('/api/announcements?page=1&pageSize=20&search=window')
  })

  it('sends the published state picked in the filter dialog on Apply and shows it as a chip', async () => {
    const mock = stubRoutes({ 'GET /api/announcements': list })
    const { wrapper, router } = await mountListAt()

    await applyListFilter(wrapper, { 'announcements-filter-published': 'draft' }, 'announcements-filter')

    expect(router.currentRoute.value.query).toEqual({ published: 'draft' })
    expect(listUrls(mock).at(-1)).toBe('/api/announcements?page=1&pageSize=20&isPublished=false')
    expect(wrapper.get('[data-testid="filter-chip-published"]').text()).toContain('Draft')
  })

  it('restores the search, the filter and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ 'GET /api/announcements': list })
    const { wrapper } = await mountListAt('?search=window&published=published&page=2')

    expect(listUrls(mock)[0]).toBe('/api/announcements?page=2&pageSize=20&search=window&isPublished=true')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('1')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubRoutes({ 'GET /api/announcements': list })
    const { wrapper } = await mountListAt('?published=maybe')

    expect(listUrls(mock)[0]).toBe('/api/announcements?page=1&pageSize=20')
    expect(wrapper.find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('removes the filter from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/announcements': list })
    const { wrapper, router } = await mountListAt('?published=draft')

    await wrapper.get('[data-testid="filter-chip-remove-published"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({})
    expect(listUrls(mock).at(-1)).toBe('/api/announcements?page=1&pageSize=20')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubRoutes({
      'GET /api/announcements': () => json({ items: [announcement], pageNumber: 1, pageSize: 20, totalCount: 60, totalPages: 3 }),
    })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="announcements-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/announcements?page=2&pageSize=20')
  })

  it('shows a narrower empty state when a filter leaves no announcement', async () => {
    stubRoutes({ 'GET /api/announcements': () => json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }) })
    const { wrapper } = await mountListAt('?published=draft')

    expect(wrapper.find('[data-testid="announcements-empty-narrowed"]').exists()).toBe(true)
  })

  it('steps back one page when a delete empties the last page', async () => {
    let deleted = false
    stubRoutes({
      'DELETE /api/announcements/3': () => {
        deleted = true
        return new Response(null, { status: 204 })
      },
      'GET /api/announcements?page=2': () =>
        json({ items: deleted ? [] : [announcement], pageNumber: 2, pageSize: 20, totalCount: deleted ? 20 : 21, totalPages: deleted ? 1 : 2 }),
      'GET /api/announcements?page=1': list,
    })
    const { wrapper, router } = await mountListAt('?page=2')

    await wrapper.get('[data-testid="announcements-3-actions"]').trigger('click')
    await wrapper.get('[data-testid="announcements-3-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.page ?? '1').toBe('1')
  })
})
