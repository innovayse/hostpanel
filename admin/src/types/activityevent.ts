/** The kinds of event the admin notification feed can surface. */
export type ActivityEventType = 'ClientRegistered' | 'InvoiceOverdue' | 'DomainExpiring'

/** A single event in the admin notification feed. */
export interface ActivityEvent {
  /** Machine-readable event kind, used to pick an icon/label. */
  type: ActivityEventType
  /** Human-readable summary of the event. */
  message: string
  /** ISO timestamp of when the event happened. */
  occurredAt: string
  /** Primary key of the client, invoice or domain this event is about. */
  entityId: number
}
