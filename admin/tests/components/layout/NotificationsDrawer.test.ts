/**
 * Mount tests for NotificationsDrawer: the unread count and "Mark all as read", the All / Unread switch, the day
 * groups, opening and marking rows through the API, the empty states, the loading rows and the pinned "View all".
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import NotificationsDrawer from '@/components/layout/NotificationsDrawer.vue'
import { useActivityStore } from '@/stores/activity'
import { requestsOf } from '../../helpers/routes'
import { mountScreen } from '../../helpers/mount'
import { activityEvent, overdueInvoice, paymentReceived, serveActivityFeed, ticketOpened } from '../../helpers/activityEvents'
import type { ActivityEvent } from '@/types/activityevent'

/** A moment of today, so the events built from it land in the Today group. */
const TODAY = new Date().toISOString()

/** A payment received today. */
const todaysPayment = { ...paymentReceived, occurredAt: TODAY }

/** A ticket opened today. */
const todaysTicket = { ...ticketOpened, occurredAt: TODAY }

/** An invoice that went overdue long ago. */
const oldInvoice = { ...overdueInvoice, occurredAt: '2020-01-01T09:00:00Z' }

/** The scripted feed: two events of today, then the old one. */
const feed = [todaysPayment, todaysTicket, oldInvoice]

/**
 * Mounts the open drawer over a scripted server and loads the feed into the store.
 *
 * @param events - What the feed endpoint answers.
 * @returns The wrapper and the stubbed server.
 */
const mountDrawer = async (events: ActivityEvent[] = feed) => {
  const server = serveActivityFeed(events)
  const { wrapper } = await mountScreen(NotificationsDrawer, { open: true })
  await useActivityStore().fetchActivity()
  await flushPromises()
  return { wrapper, server }
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('NotificationsDrawer', () => {
  it('says how many events are unread', async () => {
    const { wrapper } = await mountDrawer()

    expect(wrapper.get('[data-testid="notifications-unread-count"]').text()).toBe('3 unread')
  })

  it('groups the events by day under their headings', async () => {
    const { wrapper } = await mountDrawer()

    expect(wrapper.get('[data-testid="notifications-group-today"]').text()).toContain('Today')
    expect(wrapper.get('[data-testid="notifications-group-today"]').text()).toContain('Payment received for invoice #34')
    expect(wrapper.get('[data-testid="notifications-group-earlier"]').text()).toContain('Earlier')
    expect(wrapper.get('[data-testid="notifications-group-earlier"]').text()).toContain('Invoice #33 overdue')
    expect(wrapper.find('[data-testid="notifications-group-upcoming"]').exists()).toBe(false)
  })

  it('marks every event read on the server from the header and disables the button once none is unread', async () => {
    const { wrapper, server } = await mountDrawer()

    await wrapper.get('[data-testid="notifications-mark-all-read"]').trigger('click')
    await flushPromises()

    expect(requestsOf(server.mock)).toContain('POST /admin/dashboard/activity/read-all')
    expect(wrapper.get('[data-testid="notifications-unread-count"]').text()).toBe('0 unread')
    expect(wrapper.get('[data-testid="notifications-mark-all-read"]').attributes('disabled')).toBeDefined()
    expect(wrapper.findAll('[data-unread="true"]')).toHaveLength(0)
  })

  it('marks one event read on the server from its own button', async () => {
    const { wrapper, server } = await mountDrawer()

    await wrapper.get('[data-testid="notification-0-mark-read"]').trigger('click')
    await flushPromises()

    expect(server.read).toEqual(new Set([todaysPayment.key]))
    expect(wrapper.get('[data-testid="notification-0"]').attributes('data-unread')).toBe('false')
    expect(wrapper.get('[data-testid="notifications-unread-count"]').text()).toBe('2 unread')
  })

  it('lists only the unread events under the Unread filter', async () => {
    const { wrapper } = await mountDrawer()
    await wrapper.get('[data-testid="notification-0-mark-read"]').trigger('click')
    await flushPromises()

    await wrapper.get('[data-testid="notifications-filter-unread"]').trigger('click')

    expect(wrapper.findAll('[data-testid^="notification-"][data-unread]')).toHaveLength(2)
    expect(wrapper.text()).not.toContain('Payment received for invoice #34')

    await wrapper.get('[data-testid="notifications-filter-all"]').trigger('click')
    expect(wrapper.findAll('[data-testid^="notification-"][data-unread]')).toHaveLength(3)
  })

  it('says nothing is unread when the filter leaves nothing, and says no notifications for an empty feed', async () => {
    const { wrapper } = await mountDrawer()
    await wrapper.get('[data-testid="notifications-mark-all-read"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="notifications-filter-unread"]').trigger('click')

    expect(wrapper.get('[data-testid="notifications-empty"]').text()).toContain('No unread notifications')

    const empty = await mountDrawer([])
    expect(empty.wrapper.get('[data-testid="notifications-empty"]').text()).toContain('No notifications')
  })

  it('reads an event on the server and closes when its row is opened', async () => {
    const { wrapper, server } = await mountDrawer()

    await wrapper.get('[data-testid="notification-0-link"]').trigger('click')
    await flushPromises()

    expect(wrapper.emitted('close')).toHaveLength(1)
    expect(server.read).toEqual(new Set([todaysPayment.key]))
  })

  it('pins "View all" in the footer, linked to the full feed, and closes when it is followed', async () => {
    const { wrapper } = await mountDrawer()

    const footer = wrapper.get('[data-testid="ui-drawer-footer"]')
    const link = footer.get('[data-testid="notifications-view-all"]')
    expect(link.attributes('href')).toBe('/notifications')
    expect(link.text()).toBe('View all')

    await link.trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('draws bars in the rows while the feed loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountScreen(NotificationsDrawer, { open: true })
    void useActivityStore().fetchActivity()
    await flushPromises()

    const loading = wrapper.get('[data-testid="notifications-loading"]')
    expect(loading.attributes('aria-busy')).toBe('true')
    expect(loading.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(9)
    expect(wrapper.find('[data-testid="notifications-empty"]').exists()).toBe(false)
  })

  it('keeps the feed order inside a group', async () => {
    const { wrapper } = await mountDrawer([
      { ...activityEvent({ entityId: 1, key: 'ClientRegistered:1:1' }), occurredAt: TODAY },
      { ...activityEvent({ entityId: 2, key: 'ClientRegistered:2:1' }), occurredAt: TODAY },
    ])

    const rows = wrapper.get('[data-testid="notifications-group-today"]').findAll('[data-testid^="notification-"][data-unread]')
    expect(rows.map((row) => row.attributes('data-testid'))).toEqual(['notification-0', 'notification-1'])
  })
})
