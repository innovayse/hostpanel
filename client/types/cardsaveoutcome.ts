/**
 * How starting to save a card ended.
 *
 * @module types/cardsaveoutcome
 */

/** A closed set the tab branches on; the refusal sentence lives in the account store. */
export enum CardSaveOutcome {
  /** The card is saved. */
  Saved = 'saved',
  /** The payer has to be sent to the gateway's page; the result carries where. */
  Redirect = 'redirect',
  /** The save was refused or never finished; the reason is in the store's `paymentActionError`. */
  Failed = 'failed'
}
