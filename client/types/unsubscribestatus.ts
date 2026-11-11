/**
 * Where an unsubscribe attempt stands, held by `stores/unsubscribe.ts` for the landing page.
 */
export enum UnsubscribeStatus {
  /** The request has not finished (or has not started). */
  Pending = 'pending',
  /** The address no longer receives campaign mail. */
  Done = 'done',
  /** The API refused the link; the reason is in the store. */
  Failed = 'failed'
}
