/**
 * Every status word the client area shows in a status badge, as the API names it.
 *
 * Services, domains, invoices and tickets share one badge, so this is the union of their
 * vocabularies. The API may send a word that is not listed; the badge then shows it verbatim
 * in the neutral tone.
 *
 * @module types/badgestatus
 */
export enum BadgeStatus {
  /** Running or live. */
  Active = 'Active',
  /** An invoice that has been settled. */
  Paid = 'Paid',
  /** A ticket waiting for staff. */
  Open = 'Open',
  /** A ticket the client replied to, waiting for staff. */
  AwaitingReply = 'AwaitingReply',
  /** A ticket staff replied to. */
  Answered = 'Answered',
  /** Ordered, not finished yet. */
  Pending = 'Pending',
  /** A domain whose registration is under way. */
  PendingRegistration = 'PendingRegistration',
  /** A domain whose transfer in is under way. */
  PendingTransfer = 'PendingTransfer',
  /** A domain waiting for its DNS to propagate. */
  PendingDns = 'PendingDns',
  /** An invoice awaiting payment. */
  Unpaid = 'Unpaid',
  /** An invoice unpaid past its due date. */
  Overdue = 'Overdue',
  /** An invoice with a payment in flight. */
  PaymentPending = 'PaymentPending',
  /** An invoice not yet issued. */
  Draft = 'Draft',
  /** An invoice paid and then returned. */
  Refunded = 'Refunded',
  /** An invoice paid and then partly returned. */
  PartiallyRefunded = 'PartiallyRefunded',
  /** An invoice handed to collections. */
  Collections = 'Collections',
  /** A service switched off. */
  Suspended = 'Suspended',
  /** A service removed by staff. */
  Terminated = 'Terminated',
  /** A domain transferred out. */
  Transferred = 'Transferred',
  /** Cancelled by the client. */
  Cancelled = 'Cancelled',
  /** A ticket that is finished. */
  Closed = 'Closed',
  /** A domain past its expiry date. */
  Expired = 'Expired',
  /** A domain in its post-expiry grace period. */
  Grace = 'Grace',
  /** A domain in the registry redemption period. */
  Redemption = 'Redemption'
}
