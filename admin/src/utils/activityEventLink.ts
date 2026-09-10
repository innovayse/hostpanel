import type { ActivityEvent } from '../types/activityevent'

/**
 * Resolves where a notification-feed item should navigate to.
 *
 * A domain has no top-level detail route — only nested under its client
 * (`/clients/:id/domains/:domainId`), and the feed does not carry the owning
 * client's id — so a domain-expiring event links to the domains list instead
 * of a specific record.
 *
 * @param event - The activity event to link.
 * @returns The admin SPA route path for this event.
 */
export const activityEventLink = (event: ActivityEvent): string => {
  switch (event.type) {
    case 'ClientRegistered':
      return `/clients/${event.entityId}`
    case 'InvoiceOverdue':
      return `/billing/${event.entityId}`
    case 'DomainExpiring':
      return '/domains'
  }
}
