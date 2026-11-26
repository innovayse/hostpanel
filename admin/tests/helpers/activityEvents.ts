/**
 * Builders of notification-feed events for the tests of the drawer, the full page, the store and the topbar.
 */
import { ActivityEventType } from '@/types/activityevent'
import type { ActivityEvent } from '@/types/activityevent'
import { json } from './fetch'
import { stubRouteTable } from './routes'

/**
 * Builds a feed event; every fact not named is a plain default.
 *
 * @param overrides - The fields the test cares about.
 * @returns A client-registered event about client 7 unless the overrides say otherwise.
 */
export const activityEvent = (overrides: Partial<ActivityEvent> = {}): ActivityEvent => ({
  type: ActivityEventType.ClientRegistered,
  key: 'ClientRegistered:7:1',
  occurredAt: '2026-10-14T08:00:00Z',
  entityId: 7,
  clientId: 7,
  clientName: 'Ada Lovelace',
  subject: null,
  amount: null,
  currency: null,
  isRead: false,
  ...overrides,
})

/** An overdue invoice of 40 USD for Ada, numbered 33. */
export const overdueInvoice: ActivityEvent = activityEvent({
  type: ActivityEventType.InvoiceOverdue,
  key: 'InvoiceOverdue:33:1',
  entityId: 33,
  amount: 40,
  currency: 'USD',
})

/** A payment of 75 USD for invoice 34. */
export const paymentReceived: ActivityEvent = activityEvent({
  type: ActivityEventType.PaymentReceived,
  key: 'PaymentReceived:34:1',
  entityId: 34,
  amount: 75,
  currency: 'USD',
})

/** A ticket "Site is down" numbered 12. */
export const ticketOpened: ActivityEvent = activityEvent({
  type: ActivityEventType.TicketOpened,
  key: 'TicketOpened:12:1',
  entityId: 12,
  subject: 'Site is down',
})

/** The domain example.com about to expire. */
export const domainExpiring: ActivityEvent = activityEvent({
  type: ActivityEventType.DomainExpiring,
  key: 'DomainExpiring:3:1',
  entityId: 3,
  subject: 'example.com',
})

/** What {@link serveActivityFeed} hands back. */
export interface ServedActivityFeed {
  /** The mock `fetch`, for asserting the calls that went out. */
  mock: ReturnType<typeof stubRouteTable>
  /** The keys of the events the stubbed server holds as read. */
  read: Set<string>
}

/**
 * Stubs the notification endpoints like the server does: the feed carries `isRead` per event, and the two
 * read routes change what the next read of the feed answers.
 *
 * @param events - The events of the feed, as the server would send them unread.
 * @returns The mock and the set of keys the server holds as read.
 */
export const serveActivityFeed = (events: ActivityEvent[]): ServedActivityFeed => {
  const read = new Set<string>()
  const mock = stubRouteTable({
    'GET /admin/dashboard/activity': () => json(events.map((event) => ({ ...event, isRead: read.has(event.key) }))),
    'POST /admin/dashboard/activity/read': (body) => {
      read.add((body as { eventKey: string }).eventKey)
      return new Response(null, { status: 204 })
    },
    'POST /admin/dashboard/activity/read-all': () => {
      events.forEach((event) => read.add(event.key))
      return new Response(null, { status: 204 })
    },
  })
  return { mock, read }
}
