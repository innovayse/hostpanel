/**
 * Maps a notification-feed activity event to the admin screen it opens.
 */

import { ActivityEventType } from '@/types/activityevent'
import type { ActivityEvent } from '@/types/activityevent'
import { AppRoute, clientPath, invoicePath, supportTicketPath } from '@/lib/routes'

/**
 * Resolves where a notification-feed item should navigate to.
 *
 * A domain has no top-level detail route, only one nested under its client
 * (`/clients/:id/domains/:domainId`), so a domain-expiring event links to the domains
 * list instead of a specific record.
 *
 * @param event - The activity event to link.
 * @returns The admin SPA route path for this event.
 */
export const activityEventLink = (event: ActivityEvent): string => {
  switch (event.type) {
    case ActivityEventType.ClientRegistered:
      return clientPath(event.entityId)
    case ActivityEventType.InvoiceOverdue:
    case ActivityEventType.PaymentReceived:
      return invoicePath(event.entityId)
    case ActivityEventType.TicketOpened:
      return supportTicketPath(event.entityId)
    case ActivityEventType.DomainExpiring:
      return AppRoute.Domains
  }
}
