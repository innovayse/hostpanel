/**
 * Where an unsubscribe attempt stands, held by `stores/unsubscribe.ts` for the landing page.
 */
export enum UnsubscribeStatus {
  /** Nothing has been sent yet: the page waits for the reader to confirm. */
  Idle = 'idle',
  /** The request is on its way and has not finished. */
  Pending = 'pending',
  /** The address no longer receives campaign mail. */
  Done = 'done',
  /** The API refused the link; the reason is in the store. */
  Failed = 'failed'
}
