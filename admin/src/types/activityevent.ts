/** The kinds of event the admin notification feed can surface. */
export const ActivityEventType = {
  /** A new client signed up. */
  ClientRegistered: 'ClientRegistered',
  /** An invoice passed its due date unpaid. */
  InvoiceOverdue: 'InvoiceOverdue',
  /** A domain is close to its expiry date. */
  DomainExpiring: 'DomainExpiring',
  /** An invoice was paid. */
  PaymentReceived: 'PaymentReceived',
  /** A client opened a support ticket that still waits for staff. */
  TicketOpened: 'TicketOpened',
} as const

/** One of the {@link ActivityEventType} values. */
export type ActivityEventType = (typeof ActivityEventType)[keyof typeof ActivityEventType]

/**
 * A single event in the admin notification feed. The API sends the kind and the facts, never a
 * sentence: the screen words the title in the operator's language from the type and these facts.
 */
export interface ActivityEvent {
  /** Machine-readable event kind, which decides the icon, the colour, the wording and the link. */
  type: ActivityEventType
  /** The identity of the event, the same on every read of the feed: what a read mark is stored against. */
  key: string
  /** ISO timestamp of when the event happened (or, for an expiring domain, will happen). */
  occurredAt: string
  /** Primary key of the client, invoice, ticket or domain this event is about. */
  entityId: number
  /** Primary key of the client the event concerns (the new client itself for a registration). */
  clientId: number
  /** Full name of that client. */
  clientName: string
  /** The domain name of a domain event or the subject of a ticket; null for every other type. */
  subject: string | null
  /** The invoice total of an overdue invoice or a payment; null for every other type. */
  amount: number | null
  /** Currency code of `amount`; null when there is no amount. */
  currency: string | null
  /** Whether the signed-in admin has already read the event. */
  isRead: boolean
}
